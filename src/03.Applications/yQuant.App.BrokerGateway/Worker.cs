using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Notification.Telegram; // Assuming this will contain the notification service
using System.Reflection;

namespace yQuant.App.BrokerGateway
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceProvider _serviceProvider;
        private readonly INotificationService _telegramNotifier;
        private readonly TelegramMessageBuilder _telegramBuilder;

        private readonly IEnumerable<ITradingLogger> _tradingLoggers;
        private readonly ISystemLogger _systemLogger;

        private KISAccountManager? _kisAccountManager;
        private List<AccountConfig> _accountsToSync = new();
        private TimeSpan _syncInterval;

        public Worker(ILogger<Worker> logger, IConfiguration configuration,
            IConnectionMultiplexer redis,
            IServiceProvider serviceProvider,
            INotificationService telegramNotifier,
            TelegramMessageBuilder telegramBuilder,
            IEnumerable<ITradingLogger> tradingLoggers,
            ISystemLogger systemLogger)
        {
            _logger = logger;
            _configuration = configuration;
            _redis = redis;
            _serviceProvider = serviceProvider;
            _telegramNotifier = telegramNotifier;
            _telegramBuilder = telegramBuilder;
            _tradingLoggers = tradingLoggers;
            _systemLogger = systemLogger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var config = new BrokerGatewayConfig();
            _configuration.Bind(config);

            _accountsToSync = config.Accounts ?? new List<AccountConfig>();
            _syncInterval = TimeSpan.FromSeconds(config.SyncIntervalSeconds > 0 ? config.SyncIntervalSeconds : 1);

            _kisAccountManager = _serviceProvider.GetRequiredService<KISAccountManager>();
            LoadAccountAdapters(_accountsToSync);
            await base.StartAsync(cancellationToken);
        }

        private void LoadAccountAdapters(List<AccountConfig> accounts)
        {
            if (_kisAccountManager == null)
            {
                _logger.LogError("KISAccountManager is not initialized.");
                return;
            }

            foreach (var account in accounts)
            {
                if (account.Broker.Equals("KIS", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(account.AppKey) || string.IsNullOrEmpty(account.AppSecret))
                    {
                        _logger.LogWarning("Account {Alias} has missing credentials. Skipping.", account.Alias);
                        continue;
                    }

                    try
                    {
                        _kisAccountManager.RegisterAccount(
                            account.Alias,
                            account.AppKey,
                            account.AppSecret,
                            account.BaseUrl,
                            account.Number
                        );
                        _logger.LogInformation("Loaded KIS account: {Alias}", account.Alias);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load KIS account: {Alias}", account.Alias);
                    }
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BrokerGateway Worker started at: {time}", DateTimeOffset.Now);

            var subscriber = _redis.GetSubscriber();

            // Order Subscriber
            await subscriber.SubscribeAsync(RedisChannel.Literal("order"), async (channel, message) =>
            {
                _logger.LogInformation("Received order: {Message}", message);
                try
                {
                    var order = JsonSerializer.Deserialize<yQuant.Core.Models.Order>(message.ToString());
                    if (order == null)
                    {
                        _logger.LogWarning("Failed to deserialize order: {Message}", message);
                        return;
                    }

                    await ProcessOrder(order);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order: {Message}", message);
                }
            });

            // State Syncer
            using var timer = new PeriodicTimer(_syncInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await SyncAccountStates();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("StateSyncer stopped due to cancellation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StateSyncer encountered an error.");
            }

            _logger.LogInformation("BrokerGateway Worker stopped at: {time}", DateTimeOffset.Now);
        }

        private async Task ProcessOrder(yQuant.Core.Models.Order order)
        {
            if (string.IsNullOrEmpty(order.AccountAlias))
            {
                _logger.LogWarning("Order {OrderId} has no AccountAlias. Cannot process.", order.Id);
                return;
            }

            var accountConfig = _accountsToSync.FirstOrDefault(a => a.Alias == order.AccountAlias);
            if (accountConfig == null)
            {
                _logger.LogWarning("No configuration found for AccountAlias {AccountAlias} for order {OrderId}.", order.AccountAlias, order.Id);
                var msg = _telegramBuilder.BuildNoAccountConfigMessage(order.AccountAlias, order.Ticker);
                await _telegramNotifier.SendNotificationAsync(msg);
                return;
            }

            IBrokerAdapter? brokerAdapter = null;
            if (accountConfig.Broker.Equals("KIS", StringComparison.OrdinalIgnoreCase))
            {
                brokerAdapter = _kisAccountManager?.GetAdapter(order.AccountAlias);
            }

            if (brokerAdapter == null)
            {
                _logger.LogError("No broker adapter found for account {AccountAlias}.", order.AccountAlias);
                var msg = _telegramBuilder.BuildNoBrokerAdapterMessage(order.AccountAlias, order.Ticker);
                await _telegramNotifier.SendNotificationAsync(msg);
                return;
            }

            try
            {
                // Ensure connection is established before sending order
                await brokerAdapter.EnsureConnectedAsync();

                var orderResult = await brokerAdapter.PlaceOrderAsync(order, accountConfig.Number);
                _logger.LogInformation("Order {OrderId} placed via {Broker}. Result: {IsSuccess} - {Message}", order.Id, accountConfig.Broker, orderResult.IsSuccess, orderResult.Message);

                // Publish execution result to Redis
                var db = _redis.GetDatabase();
                var status = orderResult.IsSuccess ? "Filled" : "Rejected"; // Or "Submitted" if we want to distinguish
                var executionPayload = new { OrderId = order.Id, Status = status, Result = orderResult.Message, BrokerOrderId = orderResult.BrokerOrderId, Timestamp = DateTime.UtcNow };
                await db.PublishAsync(RedisChannel.Literal("execution"), JsonSerializer.Serialize(executionPayload));
                
                if (orderResult.IsSuccess)
                {
                    // Success: Discord ONLY (via _tradingLoggers)
                    // Telegram: None (as per user request)
                    
                    // Logging (Discord)
                    foreach (var logger in _tradingLoggers)
                    {
                        await logger.LogOrderAsync(order);
                    }
                }
                else
                {
                    // Failure: Telegram AND Discord
                    
                    // Telegram
                    var msg = _telegramBuilder.BuildOrderFailureMessage(order, orderResult.Message);
                    await _telegramNotifier.SendNotificationAsync(msg);
                    
                    // Discord (via _tradingLoggers)
                    foreach (var logger in _tradingLoggers)
                    {
                        await logger.LogOrderFailureAsync(order, orderResult.Message);
                    }

                    _logger.LogWarning("Order {OrderId} rejected: {Message}", order.Id, orderResult.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to place order {OrderId} via {Broker}.");
                
                // Exception: Telegram AND Discord
                
                var msg = _telegramBuilder.BuildOrderFailureMessage(order, ex.Message);
                await _telegramNotifier.SendNotificationAsync(msg);
                
                // Logging
                foreach (var logger in _tradingLoggers)
                {
                    await logger.LogAccountErrorAsync(order.AccountAlias, ex, $"PlaceOrder: {order.Ticker}");
                }
            }
        }

        private async Task SyncAccountStates()
        {
            var db = _redis.GetDatabase();
            foreach (var accountConfig in _accountsToSync)
            {
                IBrokerAdapter? brokerAdapter = null;
                if (accountConfig.Broker.Equals("KIS", StringComparison.OrdinalIgnoreCase))
                {
                    brokerAdapter = _kisAccountManager?.GetAdapter(accountConfig.Alias);
                }

                if (brokerAdapter == null)
                {
                    _logger.LogWarning("No broker adapter found for account {Alias}. Skipping sync.", accountConfig.Alias);
                    continue;
                }

                try
                {
                    // Ensure connection is established before syncing
                    await brokerAdapter.EnsureConnectedAsync();

                    // Sync Account State
                    var accountState = await brokerAdapter.GetAccountStateAsync(accountConfig.Number);
                    if (accountState != null)
                    {
                        // Map AccountState to yQuant.Core.Models.Account
                        var coreAccount = new Account
                        {
                            Alias = accountConfig.Alias,
                            Number = accountConfig.Number,
                            Broker = accountConfig.Broker,
                            Active = true, // Assuming active if we are syncing it
                            Deposits = new Dictionary<CurrencyType, decimal>(),
                            Positions = new List<Position>()
                        };

                        // Populate deposits from Account's Deposits dictionary
                        if (accountState.Deposits.TryGetValue(CurrencyType.KRW, out var krwDeposit))
                        {
                            coreAccount.Deposits.Add(CurrencyType.KRW, krwDeposit);
                        }
                        if (accountState.Deposits.TryGetValue(CurrencyType.USD, out var usdDeposit))
                        {
                            coreAccount.Deposits.Add(CurrencyType.USD, usdDeposit);
                        }

                        await db.StringSetAsync($"cache:account:{accountConfig.Alias}", JsonSerializer.Serialize(coreAccount), TimeSpan.FromSeconds(5));
                        _logger.LogDebug("Synced account state for {AccountId}", accountConfig.Alias);
                    }

                    // Sync Positions
                    var positions = await brokerAdapter.GetPositionsAsync(accountConfig.Number);
                    if (positions != null)
                    {
                        foreach (var pos in positions)
                        {
                            // KISAdapter returns yQuant.Core.Models.Position directly
                            await db.StringSetAsync($"cache:position:{accountConfig.Alias}:{pos.Ticker}", JsonSerializer.Serialize(pos), TimeSpan.FromSeconds(5));
                            _logger.LogDebug("Synced position for {AccountId}: {Ticker}", accountConfig.Alias, pos.Ticker);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync state for account {AccountId} with broker {Broker}.", accountConfig.Alias, accountConfig.Broker);
                    var msg = _telegramBuilder.BuildAccountSyncFailureMessage(accountConfig.Alias, ex.Message);
                    await _telegramNotifier.SendNotificationAsync(msg);
                    
                    // Logging
                    foreach (var logger in _tradingLoggers)
                    {
                        await logger.LogAccountErrorAsync(accountConfig.Alias, ex, "SyncAccountStates");
                    }
                }
            }
        }


        // Configuration classes
        public class BrokerGatewayConfig
        {
            public List<AccountConfig>? Accounts { get; set; }
            public TelegramConfig? Telegram { get; set; }
            public int SyncIntervalSeconds { get; set; }
        }

        public class AccountConfig
        {
            public string Alias { get; set; } = string.Empty;  // Internal account identifier
            public string Broker { get; set; } = string.Empty;
            public string Number { get; set; } = string.Empty;
            public string AppKey { get; set; } = string.Empty;
            public string AppSecret { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
        }

        public class TelegramConfig
        {
            public string BotToken { get; set; } = string.Empty;
            public string ChatId { get; set; } = string.Empty;
        }
    }
}