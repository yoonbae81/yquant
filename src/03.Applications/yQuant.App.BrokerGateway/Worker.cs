using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Notification.Telegram;
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

        private readonly Dictionary<string, IBrokerAdapter> _adapters;
        private TimeSpan _syncInterval;

        public Worker(ILogger<Worker> logger, IConfiguration configuration,
            IConnectionMultiplexer redis,
            IServiceProvider serviceProvider,
            INotificationService telegramNotifier,
            TelegramMessageBuilder telegramBuilder,
            IEnumerable<ITradingLogger> tradingLoggers,
            ISystemLogger systemLogger,
            Dictionary<string, IBrokerAdapter> adapters)
        {
            _logger = logger;
            _configuration = configuration;
            _redis = redis;
            _serviceProvider = serviceProvider;
            _telegramNotifier = telegramNotifier;
            _telegramBuilder = telegramBuilder;
            _tradingLoggers = tradingLoggers;
            _systemLogger = systemLogger;
            _adapters = adapters;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var syncIntervalSeconds = _configuration.GetValue<int>("SyncIntervalSeconds");
            _syncInterval = TimeSpan.FromSeconds(syncIntervalSeconds > 0 ? syncIntervalSeconds : 1);

            await base.StartAsync(cancellationToken);
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

            // Look up adapter directly
            if (!_adapters.TryGetValue(order.AccountAlias, out var brokerAdapter))
            {
                _logger.LogWarning("No broker adapter found for account {AccountAlias}.", order.AccountAlias);
                var msg = _telegramBuilder.BuildNoBrokerAdapterMessage(order.AccountAlias, order.Ticker);
                await _telegramNotifier.SendNotificationAsync(msg);
                return;
            }

            var account = brokerAdapter.Account;

            try
            {
                // Ensure connection is established before sending order
                await brokerAdapter.EnsureConnectedAsync();

                var orderResult = await brokerAdapter.PlaceOrderAsync(order);
                _logger.LogInformation("Order {OrderId} placed via {Broker}. Result: {IsSuccess} - {Message}", order.Id, account.Broker, orderResult.IsSuccess, orderResult.Message);

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
            foreach (var alias in _adapters.Keys)
            {
                var brokerAdapter = _adapters[alias];
                var account = brokerAdapter.Account;

                try
                {
                    // Ensure connection is established before syncing
                    await brokerAdapter.EnsureConnectedAsync();

                    // Sync Account State
                    var accountState = await brokerAdapter.GetAccountStateAsync();
                    if (accountState != null)
                    {
                        // Map AccountState to yQuant.Core.Models.Account
                        // We can reuse the existing account object but it's safer to create a DTO or update the existing one carefully
                        // For now, let's update the deposits on the existing account object and serialize it
                        
                        account.Deposits.Clear(); // Clear old deposits
                        if (accountState.Deposits.TryGetValue(CurrencyType.KRW, out var krwDeposit))
                        {
                            account.Deposits.Add(CurrencyType.KRW, krwDeposit);
                        }
                        if (accountState.Deposits.TryGetValue(CurrencyType.USD, out var usdDeposit))
                        {
                            account.Deposits.Add(CurrencyType.USD, usdDeposit);
                        }

                        await db.StringSetAsync($"cache:account:{alias}", JsonSerializer.Serialize(account), TimeSpan.FromSeconds(5));
                        _logger.LogDebug("Synced account state for {AccountId}", alias);
                    }

                    // Sync Positions
                    var positions = await brokerAdapter.GetPositionsAsync();
                    if (positions != null)
                    {
                        foreach (var pos in positions)
                        {
                            // KISAdapter returns yQuant.Core.Models.Position directly
                            await db.StringSetAsync($"cache:position:{alias}:{pos.Ticker}", JsonSerializer.Serialize(pos), TimeSpan.FromSeconds(5));
                            _logger.LogDebug("Synced position for {AccountId}: {Ticker}", alias, pos.Ticker);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync state for account {AccountId} with broker {Broker}.", alias, account.Broker);
                    var msg = _telegramBuilder.BuildAccountSyncFailureMessage(alias, ex.Message);
                    await _telegramNotifier.SendNotificationAsync(msg);
                    
                    // Logging
                    foreach (var logger in _tradingLoggers)
                    {
                        await logger.LogAccountErrorAsync(alias, ex, "SyncAccountStates");
                    }
                }
            }
        }
    }
}