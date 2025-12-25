using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using yQuant.App.OrderManager.Models;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using CoreOrder = yQuant.Core.Models.Order;

namespace yQuant.App.OrderManager.Services;

/// <summary>
/// Executes scheduled orders and liquidations by reading from Redis and publishing to order channel
/// </summary>
public class ScheduleExecutor
{
    private readonly ILogger<ScheduleExecutor> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IOrderPublisher _orderPublisher;
    private readonly yQuant.Infra.Notification.Discord.DiscordLogger? _discordLogger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ScheduleExecutor(
        ILogger<ScheduleExecutor> logger,
        IConnectionMultiplexer redis,
        IOrderPublisher orderPublisher,
        yQuant.Infra.Notification.Discord.DiscordLogger? discordLogger = null)
    {
        _logger = logger;
        _redis = redis;
        _orderPublisher = orderPublisher;
        _discordLogger = discordLogger;
    }

    public async Task ProcessSchedulesAsync()
    {
        try
        {
            await ProcessScheduledOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled orders");
        }
    }

    private async Task ProcessScheduledOrdersAsync()
    {
        var db = _redis.GetDatabase();
        var now = DateTime.UtcNow;

        // Get all accounts
        var accounts = await db.SetMembersAsync("account:index");

        foreach (var account in accounts)
        {
            var accountAlias = account.ToString();
            var orderKey = $"scheduled:{accountAlias}";
            var orderJson = await db.StringGetAsync(orderKey);

            if (!orderJson.HasValue)
                continue;

            try
            {
                var orders = JsonSerializer.Deserialize<List<ScheduledOrder>>(orderJson.ToString(), _jsonOptions);
                if (orders == null)
                    continue;

                bool modified = false;

                foreach (var order in orders.Where(o => o.IsActive))
                {
                    // Calculate next execution time if not set
                    if (order.NextExecutionTime == null)
                    {
                        order.NextExecutionTime = CalculateNextExecutionTime(order);
                        modified = true;
                        // Skip execution on first initialization - just set the next time
                        continue;
                    }

                    // Check if it's time to execute
                    if (order.NextExecutionTime != null && order.NextExecutionTime <= now)
                    {
                        await ExecuteScheduledOrderAsync(order);

                        // Update execution times
                        order.LastExecutedTime = now;
                        order.NextExecutionTime = CalculateNextExecutionTime(order);
                        modified = true;
                    }
                }

                // Save back to Redis if modified
                if (modified)
                {
                    var updatedJson = JsonSerializer.Serialize(orders, _jsonOptions);
                    await db.StringSetAsync(orderKey, updatedJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduled orders for account {Account}", accountAlias);
            }
        }
    }

    private async Task ExecuteScheduledOrderAsync(ScheduledOrder order)
    {
        _logger.LogInformation("Executing scheduled order: {Ticker} ({Exchange}) for {Account}",
            order.Ticker, order.Exchange, order.AccountAlias);

        if (order.Quantity == null || order.Quantity <= 0)
        {
            _logger.LogWarning("Skipping scheduled order {Id} due to invalid quantity", order.Id);
            return;
        }

        // Send Discord notification: Scheduled Order Request
        if (_discordLogger != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _discordLogger.LogScheduledOrderRequestAsync(
                        order.Id.ToString(),
                        order.Ticker,
                        order.Exchange.ToString(),
                        order.Action.ToString(),
                        order.Quantity.Value,
                        order.AccountAlias,
                        order.NextExecutionTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send Discord notification for scheduled order request");
                }
            });
        }

        var coreOrder = new CoreOrder
        {
            AccountAlias = order.AccountAlias,
            Ticker = order.Ticker,
            Exchange = order.Exchange,
            Currency = order.Currency,
            Action = order.Action,
            Qty = order.Quantity.Value,
            Type = OrderType.Market,
            BuyReason = "Schedule"
        };

        try
        {
            await _orderPublisher.PublishOrderAsync(coreOrder);
            _logger.LogInformation("Successfully published scheduled order for {Ticker}", order.Ticker);

            // Send Discord notification: Scheduled Order Success
            if (_discordLogger != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordLogger.LogScheduledOrderSuccessAsync(
                            order.Id.ToString(),
                            order.Ticker,
                            order.Exchange.ToString(),
                            order.Action.ToString(),
                            order.Quantity.Value,
                            order.AccountAlias);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send Discord notification for scheduled order success");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish scheduled order {Id}", order.Id);

            // Send Discord notification: Scheduled Order Failure
            if (_discordLogger != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordLogger.LogScheduledOrderFailureAsync(
                            order.Id.ToString(),
                            order.Ticker,
                            order.Exchange.ToString(),
                            order.Action.ToString(),
                            order.Quantity.Value,
                            order.AccountAlias,
                            ex.Message);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogError(notifyEx, "Failed to send Discord notification for scheduled order failure");
                    }
                });
            }
        }
    }

    private DateTime? CalculateNextExecutionTime(ScheduledOrder order)
    {
        if (order.DaysOfWeek == null || !order.DaysOfWeek.Any())
        {
            _logger.LogWarning("ScheduledOrder {Id} has no DaysOfWeek configured", order.Id);
            return null;
        }

        var now = DateTime.UtcNow;
        var today = now.Date;

        // Check next 14 days
        for (int i = 0; i < 14; i++)
        {
            var checkDate = today.AddDays(i);

            if (order.DaysOfWeek.Contains(checkDate.DayOfWeek))
            {
                var executionTime = GetTimeForDate(order, checkDate);

                if (executionTime > now)
                {
                    return executionTime;
                }
            }
        }

        return null;
    }

    private DateTime GetTimeForDate(ScheduledOrder order, DateTime date)
    {
        if (order.TimeMode == ScheduleTimeMode.FixedTime)
        {
            return date.Add(order.TimeConfig);
        }
        else
        {
            // Market Offset
            var marketOpen = GetMarketOpenTimeUTC(order.Exchange);
            var marketOpenDateTime = date.Add(marketOpen.TimeOfDay);
            return marketOpenDateTime.Add(order.TimeConfig);
        }
    }

    private DateTime GetMarketOpenTimeUTC(ExchangeCode exchange)
    {
        // Simple Approximation
        return exchange switch
        {
            ExchangeCode.KOSPI or ExchangeCode.KOSDAQ or ExchangeCode.KRX => DateTime.UtcNow.Date.AddHours(0), // 09:00 KST = 00:00 UTC
            ExchangeCode.NASDAQ or ExchangeCode.NYSE or ExchangeCode.AMEX => DateTime.UtcNow.Date.AddHours(14.5), // 09:30 ET = 14:30 UTC
            ExchangeCode.TSE => DateTime.UtcNow.Date.AddHours(0), // 09:00 JST = 00:00 UTC
            ExchangeCode.HKEX => DateTime.UtcNow.Date.AddHours(1.5), // 09:30 HKT = 01:30 UTC
            ExchangeCode.SSE or ExchangeCode.SZSE => DateTime.UtcNow.Date.AddHours(1.5), // 09:30 CST = 01:30 UTC
            ExchangeCode.HOSE or ExchangeCode.HNX => DateTime.UtcNow.Date.AddHours(2.25), // 09:15 ICT = 02:15 UTC
            _ => DateTime.UtcNow.Date.AddHours(14.5) // Default to US
        };
    }
}
