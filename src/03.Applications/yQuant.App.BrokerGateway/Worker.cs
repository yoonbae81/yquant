using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Notification.Telegram;
using yQuant.Infra.Redis.Models;
using Order = yQuant.Core.Models.Order;

namespace yQuant.App.BrokerGateway
{
    public class Worker(
        ILogger<Worker> logger,
        IConnectionMultiplexer redis,
        Dictionary<string, IBrokerAdapter> adapters,
        INotificationService telegramNotifier,
        TelegramMessageBuilder telegramBuilder,
        IEnumerable<ITradingLogger> tradingLoggers) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly Dictionary<string, IBrokerAdapter> _adapters = adapters;
        private readonly INotificationService _telegramNotifier = telegramNotifier;
        private readonly TelegramMessageBuilder _telegramBuilder = telegramBuilder;
        private readonly IEnumerable<ITradingLogger> _tradingLoggers = tradingLoggers;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BrokerGateway Worker started.");
            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(RedisChannel.Literal("broker:requests"), (channel, message) =>
            {
                // Handle concurrently
                _ = HandleRequestAsync(message);
            });

            // Periodic Account Registration (Heartbeat)
            while (!stoppingToken.IsCancellationRequested)
            {
                await RegisterAccountsAsync();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task RegisterAccountsAsync()
        {
            try
            {
                var db = _redis.GetDatabase();
                var accounts = _adapters.Keys.ToList();
                var json = JsonSerializer.Serialize(accounts);

                await db.StringSetAsync("broker:accounts", json, TimeSpan.FromHours(24));
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Registered available accounts in Redis: {Accounts}", string.Join(", ", accounts));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register accounts in Redis.");
            }
        }

        private async Task HandleRequestAsync(RedisValue message)
        {
            try
            {
                var request = JsonSerializer.Deserialize<BrokerRequest>(message.ToString());
                if (request == null) return;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Received request {RequestId} of type {Type} for {Account}", request.Id, request.Type, request.Account);
                }

                BrokerResponse response;
                try
                {
                    response = await ProcessRequestAsync(request);
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Error processing request {RequestId}", request.Id);
                    }
                    response = new BrokerResponse
                    {
                        RequestId = request.Id,
                        Success = false,
                        Message = $"Internal Error: {ex.Message}"
                    };
                }

                if (!string.IsNullOrEmpty(request.ResponseChannel))
                {
                    var db = _redis.GetDatabase();
                    await db.PublishAsync(RedisChannel.Literal(request.ResponseChannel), JsonSerializer.Serialize(response));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize or handle request wrapper.");
            }
        }

        private async Task<BrokerResponse> ProcessRequestAsync(BrokerRequest request)
        {
            // 1. Validate Adapter
            if (!_adapters.TryGetValue(request.Account, out var adapter))
            {
                return new BrokerResponse { RequestId = request.Id, Success = false, Message = $"Account '{request.Account}' not found." };
            }

            // 2. Dispatch
            return request.Type switch
            {
                BrokerRequestType.Ping => await HandlePingAsync(adapter),
                BrokerRequestType.GetPrice => await HandleGetPriceAsync(adapter, request),
                BrokerRequestType.GetDeposit => await HandleGetDepositAsync(adapter, request),
                BrokerRequestType.GetPositions => await HandleGetPositionsAsync(adapter, request),
                BrokerRequestType.PlaceOrder => await HandlePlaceOrderAsync(adapter, request),
                _ => new BrokerResponse { RequestId = request.Id, Success = false, Message = "Unknown request type." }
            };
        }

        private static async Task<BrokerResponse> HandlePingAsync(IBrokerAdapter adapter)
        {
            try
            {
                await adapter.EnsureConnectedAsync();
                return new BrokerResponse { Success = true, Message = "Pong" };
            }
            catch (Exception ex)
            {
                return new BrokerResponse { Success = false, Message = $"Token Error: {ex.Message}" };
            }
        }

        private async Task<BrokerResponse> HandleGetPriceAsync(IBrokerAdapter adapter, BrokerRequest request)
        {
            var ticker = request.Payload;
            if (string.IsNullOrEmpty(ticker)) return new BrokerResponse { RequestId = request.Id, Success = false, Message = "Ticker missing." };

            var db = _redis.GetDatabase();
            var cacheKey = $"cache:price:{ticker}";

            // Cache Lookup
            if (!request.ForceRefresh)
            {
                var cached = await db.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    return new BrokerResponse
                    {
                        RequestId = request.Id,
                        Success = true,
                        Payload = cached.ToString()
                    };
                }
            }

            // Fetch from Broker
            try
            {
                var priceInfo = await adapter.GetPriceAsync(ticker);
                var json = JsonSerializer.Serialize(priceInfo);

                // Cache Result (TTL 5s)
                await db.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(5));

                return new BrokerResponse
                {
                    RequestId = request.Id,
                    Success = true,
                    Payload = json
                };
            }
            catch (Exception ex)
            {
                return new BrokerResponse { RequestId = request.Id, Success = false, Message = $"Price Error: {ex.Message}" };
            }
        }

        private static async Task<BrokerResponse> HandleGetDepositAsync(IBrokerAdapter adapter, BrokerRequest request)
        {
            try
            {
                CurrencyType? currencyType = null;
                if (!string.IsNullOrEmpty(request.Payload) && Enum.TryParse<CurrencyType>(request.Payload, true, out var parsed))
                {
                    currencyType = parsed;
                }

                var account = await adapter.GetDepositAsync(currencyType);
                return new BrokerResponse
                {
                    RequestId = request.Id,
                    Success = true,
                    Payload = JsonSerializer.Serialize(account)
                };
            }
            catch (Exception ex)
            {
                return new BrokerResponse { RequestId = request.Id, Success = false, Message = $"Deposit Error: {ex.Message}" };
            }
        }

        private static async Task<BrokerResponse> HandleGetPositionsAsync(IBrokerAdapter adapter, BrokerRequest request)
        {
            try
            {
                var positions = await adapter.GetPositionsAsync();
                return new BrokerResponse
                {
                    RequestId = request.Id,
                    Success = true,
                    Payload = JsonSerializer.Serialize(positions)
                };
            }
            catch (Exception ex)
            {
                return new BrokerResponse { RequestId = request.Id, Success = false, Message = $"Positions Error: {ex.Message}" };
            }
        }

        private async Task<BrokerResponse> HandlePlaceOrderAsync(IBrokerAdapter adapter, BrokerRequest request)
        {
            var order = JsonSerializer.Deserialize<Order>(request.Payload);
            if (order == null) return new BrokerResponse { RequestId = request.Id, Success = false, Message = "Invalid Order payload." };

            try
            {
                var result = await adapter.PlaceOrderAsync(order);

                // Log Result
                if (result.IsSuccess)
                {
                    foreach (var logger in _tradingLoggers) await logger.LogOrderAsync(order);
                }
                else
                {
                    foreach (var logger in _tradingLoggers) await logger.LogOrderFailureAsync(order, result.Message);
                }

                return new BrokerResponse
                {
                    RequestId = request.Id,
                    Success = true, // Request processed successfully (even if order rejected by broker)
                    Payload = JsonSerializer.Serialize(result)
                };
            }
            catch (Exception ex)
            {
                foreach (var logger in _tradingLoggers) await logger.LogAccountErrorAsync(request.Account, ex, "PlaceOrder");
                return new BrokerResponse { RequestId = request.Id, Success = false, Message = $"Order Exception: {ex.Message}" };
            }
        }
    }
}