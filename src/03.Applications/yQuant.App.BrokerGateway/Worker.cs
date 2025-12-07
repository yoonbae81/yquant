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
        IEnumerable<ITradingLogger> tradingLoggers,
        IConfiguration configuration) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly Dictionary<string, IBrokerAdapter> _adapters = adapters;
        private readonly INotificationService _telegramNotifier = telegramNotifier;
        private readonly TelegramMessageBuilder _telegramBuilder = telegramBuilder;
        private readonly IEnumerable<ITradingLogger> _tradingLoggers = tradingLoggers;
        private readonly IConfiguration _configuration = configuration;

        // Track last sync time per account (Alias -> DateTime)
        private readonly Dictionary<string, DateTime> _lastAccountSyncTime = new();

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BrokerGateway Worker starting...");
            await SyncAllAccountDataAsync(silent: false);
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BrokerGateway Worker started.");
            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(RedisChannel.Literal("order"), (channel, message) =>
            {
                // Handle concurrently
                _ = HandleRequestAsync(message);
            });


            // Periodic Account Sync Removed (Event-driven only)
            // But we need to keep the process alive
            await Task.Delay(-1, stoppingToken);
        }

        private async Task SyncAllAccountDataAsync(bool silent = false)
        {
            try
            {
                var db = _redis.GetDatabase();

                foreach (var kvp in _adapters)
                {
                    var alias = kvp.Key;
                    var adapter = kvp.Value;

                    // 1. Sync Static Account Info (account:{alias}) - can run independently
                    await SyncAccountInfoAsync(db, alias, adapter);

                    // 2. Sync Deposits first (deposit:{alias}) - calls DomesticBalance and caches it
                    await SyncDepositsAsync(db, alias, adapter);

                    // 3. Sync Positions (position:{alias}) and Prices (stock:{ticker}) - reuses cached DomesticBalance
                    await SyncPositionsAsync(db, alias, adapter);
                }

                if (!silent && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Synced all account data to Redis for: {Accounts}", string.Join(", ", _adapters.Keys));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync account data to Redis.");
            }
        }

        private async Task SyncAccountInfoAsync(IDatabase db, string alias, IBrokerAdapter adapter)
        {
            try
            {
                var account = adapter.Account;
                var key = $"account:{alias}";
                await db.HashSetAsync(key, new HashEntry[]
                {
                    new("number", account.Number),
                    new("broker", account.Broker),
                    new("is_active", account.Active.ToString())
                });

                // Add to Index
                await db.SetAddAsync("account:index", alias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync account info for {Alias}", alias);
            }
        }

        private async Task SyncDepositsAsync(IDatabase db, string alias, IBrokerAdapter adapter)
        {
            try
            {
                // Force refresh to get latest from broker
                var accountData = await adapter.GetDepositAsync(forceRefresh: true);
                var key = $"deposit:{alias}";

                var entries = accountData.Deposits.Select(d => new HashEntry(d.Key.ToString(), d.Value.ToString())).ToArray();
                if (entries.Length > 0)
                {
                    await db.HashSetAsync(key, entries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync deposits for {Alias}", alias);
            }
        }

        private async Task SyncPositionsAsync(IDatabase db, string alias, IBrokerAdapter adapter)
        {
            try
            {
                var positions = await adapter.GetPositionsAsync();
                var key = $"position:{alias}";

                // Clear old positions first? Or just overwrite?
                // If we sold everything, we need to remove the field.
                // Redis Hash doesn't support "replace all".
                // Strategy: Delete key then set new? Or strict field management.
                // For simplicity/safety, let's delete and re-set to avoid ghost positions.
                // But deleting might cause a split-second "no position" state.
                // Better: Get existing fields, calculate diff. 
                // For now, let's assume overwriting is fine, but we need to handle sold positions.
                // Let's use Delete-then-Set for correctness of "current state".

                await db.KeyDeleteAsync(key);

                if (positions.Any())
                {
                    var entries = positions.Select(p => new HashEntry(p.Ticker, JsonSerializer.Serialize(p))).ToArray();
                    await db.HashSetAsync(key, entries);

                    // Also sync prices for held positions (no need for separate API calls)
                    foreach (var position in positions)
                    {
                        try
                        {
                            var priceKey = $"stock:{position.Ticker}";

                            // Use price data already in position (from GetPositionsAsync)
                            await db.HashSetAsync(priceKey, new HashEntry[]
                            {
                                new("price", position.CurrentPrice.ToString()),
                                new("changeRate", position.ChangeRate.ToString())
                            });

                            // Refresh TTL (25 hours)
                            await db.KeyExpireAsync(priceKey, TimeSpan.FromHours(25));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to sync price for {Ticker}", position.Ticker);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync positions for {Alias}", alias);
            }
        }



        private async Task HandleRequestAsync(RedisValue message)
        {
            try
            {
                // Note: Payload is now 'Order' object directly for 'order' channel?
                // No, the schema says 'order' channel payload is 'Order'.
                // But the previous code used 'BrokerRequest' wrapper.
                // The new design says "Payload Model: Order".
                // So we need to deserialize 'Order' directly.
                // BUT, we need to know WHICH ACCOUNT to use.
                // 'Order' model has 'AccountAlias'.

                var order = JsonSerializer.Deserialize<Order>(message.ToString());
                if (order == null) return;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Received order {OrderId} for {Account}", order.Id, order.AccountAlias);
                }

                // The schema says 'execution' channel payload is 'OrderResult'.
                // So we should process the order and publish OrderResult.

                try
                {
                    var result = await ProcessOrderAsync(order);

                    // Publish to 'execution'
                    var db = _redis.GetDatabase();
                    await db.PublishAsync(RedisChannel.Literal("execution"), JsonSerializer.Serialize(result));

                    // Event-driven Sync: Update Deposits and Positions immediately after order execution
                    // THROTTLING LOGIC: Only fetch from broker if interval has passed. Otherwise, update locally to redis.

                    int updateIntervalMinutes = _configuration.GetValue<int>("AccountUpdateIntervalMinutes", 1);
                    var now = DateTime.UtcNow;

                    if (_adapters.TryGetValue(order.AccountAlias, out var adapter))
                    {
                        if (!_lastAccountSyncTime.TryGetValue(order.AccountAlias, out var lastSync) || (now - lastSync).TotalMinutes >= updateIntervalMinutes)
                        {
                            _logger.LogInformation("Account sync interval passed. Performing full broker sync for {Account}.", order.AccountAlias);
                            await SyncDepositsAsync(db, order.AccountAlias, adapter);
                            await SyncPositionsAsync(db, order.AccountAlias, adapter); // Also syncs prices

                            _lastAccountSyncTime[order.AccountAlias] = now;
                        }
                        else
                        {
                            _logger.LogInformation("Throttling account sync. Performing local Redis update for {Account}.", order.AccountAlias);
                            await UpdateDepositLocalAsync(db, order.AccountAlias, order);
                            await UpdatePositionLocalAsync(db, order.AccountAlias, order);
                            // No price sync needed for local update, or we accept stale/estimated prices
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
                    // Publish failure result
                    var failureResult = new yQuant.Core.Models.OrderResult
                    {
                        OrderId = order.Id.ToString(),
                        IsSuccess = false,
                        Message = ex.Message
                    };
                    var db = _redis.GetDatabase();
                    await db.PublishAsync(RedisChannel.Literal("execution"), JsonSerializer.Serialize(failureResult));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize or handle order message.");
            }
        }

        private async Task<yQuant.Core.Models.OrderResult> ProcessOrderAsync(Order order)
        {
            // 1. Validate Adapter
            if (!_adapters.TryGetValue(order.AccountAlias, out var adapter))
            {
                return new yQuant.Core.Models.OrderResult { OrderId = order.Id.ToString(), IsSuccess = false, Message = $"Account '{order.AccountAlias}' not found." };
            }

            // 2. Execute
            // Note: The previous code handled multiple request types (GetPrice, etc.) via 'broker:requests'.
            // The new design splits 'order' channel specifically for ORDERS.
            // What about GetPrice/GetDeposit requests from other apps?
            // The design says "Reader: App.Dashboard, App.OrderComposer" for keys.
            // So they should READ keys directly, not ask BrokerGateway via channel.
            // EXCEPT for 'Price' which might need on-demand fetch if cache miss?
            // The design says "Writer: App.BrokerGateway (On-demand / Stream)".
            // If Dashboard needs price, it reads 'stock:{ticker}'. If missing/stale?
            // For now, let's assume the periodic sync or some other mechanism handles it, 
            // OR we keep the 'broker:requests' for RPC-like calls if needed?
            // The user said "execution의 publish는 dashboard도 추가... manual order...".
            // This implies Dashboard places orders via 'order' channel.
            // It doesn't explicitly say how to get fresh price on demand.
            // But let's stick to the "Order" channel handling only Orders for now as per schema.

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

                return result;
            }
            catch (Exception ex)
            {
                foreach (var logger in _tradingLoggers) await logger.LogAccountErrorAsync(order.AccountAlias, ex, "PlaceOrder");
                return new yQuant.Core.Models.OrderResult { OrderId = order.Id.ToString(), IsSuccess = false, Message = $"Order Exception: {ex.Message}" };
            }
        }

        // Note: HandleGetPriceAsync is no longer called via channel. 
        // But we might want to expose a method or keep it for internal use if we want to support on-demand price fetch?
        // For strict adherence to the new schema which relies on 'stock:{ticker}' cache,
        // we should ensure prices are updated.
        // If we don't have an incoming request for price, we rely on... what?
        // Maybe we should subscribe to a 'price_request' channel? Or just rely on periodic updates?
        // Given the instructions, I will remove the old Request/Response logic and focus on Order processing.
        // BUT, I need to implement 'SyncPricesAsync' or similar if I want to keep prices fresh.
        // However, iterating ALL stocks to update prices is too heavy.
        // Usually, we only update prices for stocks we hold or watch.
        // For now, I will leave out the "On-demand" price fetch logic as it's not triggered by 'order' channel.
        // If the user wants on-demand price, they might need a separate channel or mechanism not fully detailed yet.
        // I will focus on the explicit requirements: Order placement and Account Sync.
        private async Task UpdateDepositLocalAsync(IDatabase db, string alias, Order order)
        {
            try
            {
                var key = $"deposit:{alias}";

                // Estimate amount: Qty * Price
                // If Market Order (Price is null/0), try to get last price from Redis
                decimal executionPrice = order.Price ?? 0;
                if (executionPrice == 0)
                {
                    // Try fetch from stock:{ticker}
                    var priceVal = await db.HashGetAsync($"stock:{order.Ticker}", "price");
                    if (priceVal.HasValue && decimal.TryParse(priceVal.ToString(), out var p))
                    {
                        executionPrice = p;
                    }
                }

                if (executionPrice == 0)
                {
                    _logger.LogWarning("Could not estimate execution price for local deposit update. Skipping.");
                    return;
                }

                decimal amountChange = order.Qty * executionPrice;
                // Add some estimated fee? (Ignored for now to match requirement "simple local update")

                // Update Logic:
                // Buy -> Decrease Currency (e.g. USD)
                // Sell -> Increase Currency
                // Note: This assumes simple Cash account. Margin/Credit logic is complex and skipped here.

                // Determine Currency to update.
                // Order.Currency is usually the Settlement Currency (e.g. USD for US Stk)

                var currencyField = order.Currency.ToString();

                // Get current value
                var currentVal = await db.HashGetAsync(key, currencyField);
                decimal currentAmount = 0;
                if (currentVal.HasValue) decimal.TryParse(currentVal.ToString(), out currentAmount);

                if (order.Action == OrderAction.Buy)
                {
                    currentAmount -= amountChange;
                }
                else
                {
                    currentAmount += amountChange;
                }

                await db.HashSetAsync(key, currencyField, currentAmount.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform local deposit update for {Alias}", alias);
            }
        }

        private async Task UpdatePositionLocalAsync(IDatabase db, string alias, Order order)
        {
            try
            {
                var key = $"position:{alias}";

                // Get existing position for this ticker
                // Redis Hash Field: Ticker
                var posJson = await db.HashGetAsync(key, order.Ticker);
                Position position;

                if (posJson.HasValue)
                {
                    position = JsonSerializer.Deserialize<Position>(posJson.ToString()) ?? new Position
                    {
                        AccountAlias = alias,
                        Ticker = order.Ticker,
                        Currency = order.Currency,
                        Qty = 0,
                        AvgPrice = 0,
                        CurrentPrice = 0
                    };
                }
                else
                {
                    // New Position
                    position = new Position
                    {
                        AccountAlias = alias,
                        Ticker = order.Ticker,
                        Currency = order.Currency,
                        Qty = 0,
                        AvgPrice = 0, // Will be set below
                        CurrentPrice = order.Price ?? 0
                    };
                }

                // Update Qty
                if (order.Action == OrderAction.Buy)
                {
                    // Update Avg Price for Buy
                    // NewAvg = ((OldQty * OldAvg) + (NewQty * NewPrice)) / (OldQty + NewQty)
                    decimal executionPrice = order.Price ?? position.CurrentPrice;
                    // If prediction failed, use 0? No, try to get from Redis stock price if still 0
                    if (executionPrice == 0)
                    {
                        var pVal = await db.HashGetAsync($"stock:{order.Ticker}", "price");
                        if (pVal.HasValue) decimal.TryParse(pVal.ToString(), out executionPrice);
                    }

                    if (position.Qty + order.Qty > 0)
                    {
                        position.AvgPrice = ((position.Qty * position.AvgPrice) + (order.Qty * executionPrice)) / (position.Qty + order.Qty);
                    }
                    else
                    {
                        // Should not happen for Buy unless short covering? Assuming Long only for now.
                        position.AvgPrice = executionPrice;
                    }
                    position.Qty += order.Qty;
                }
                else // Sell
                {
                    // Avg Price usually doesn't change on Sell (FIFO/Avg Cost), only Qty reduces.
                    // Real PnL is realized.
                    position.Qty -= order.Qty;
                }

                // If Qty <= 0, remove or keep 0?
                // Logic says: "position also... redis only change quantity".
                // If 0, we can keep it as 0 or remove. 
                // KIS usually returns empty list if no position.
                // Let's keep it as 0 to avoid "Key not found" issues until next sync clears it.
                // Or better, if 0, remove the field to match "No Position".

                if (position.Qty <= 0)
                {
                    // If we sold more than we have (Short?), let it be negative?
                    // Assuming Long-only:
                    if (position.Qty == 0)
                    {
                        await db.HashDeleteAsync(key, order.Ticker);
                        return;
                    }
                }

                await db.HashSetAsync(key, order.Ticker, JsonSerializer.Serialize(position));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform local position update for {Alias}", alias);
            }
        }
    }
}