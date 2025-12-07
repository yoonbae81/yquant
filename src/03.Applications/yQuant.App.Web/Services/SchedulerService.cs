using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using yQuant.App.Web.Models;
using yQuant.Core.Models;

namespace yQuant.App.Web.Services;

public class SchedulerService : BackgroundService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly OrderPublisher _orderPublisher;
    private readonly IConnectionMultiplexer _redis;
    private readonly List<ScheduledOrder> _scheduledOrders = new();
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Event raised when internal state changes (for UI update)
    public event Action? OnChange;

    public SchedulerService(
        ILogger<SchedulerService> logger,
        OrderPublisher orderPublisher,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _orderPublisher = orderPublisher;
        _redis = redis;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadFromRedisAsync();
        await base.StartAsync(cancellationToken);
    }

    private async Task LoadFromRedisAsync()
    {
        try
        {
            var db = _redis.GetDatabase();

            // 1. Get All Accounts
            var accounts = await db.SetMembersAsync("account:index");
            var allOrders = new List<ScheduledOrder>();

            foreach (var account in accounts)
            {
                var key = $"scheduled:{account}";
                var json = await db.StringGetAsync(key);
                if (json.HasValue)
                {
                    try
                    {
                        var orders = JsonSerializer.Deserialize<List<ScheduledOrder>>(json.ToString(), _jsonOptions);
                        if (orders != null) allOrders.AddRange(orders);
                    }
                    catch
                    {
                        _logger.LogWarning("Failed to deserialize scheduled orders for {Account}", account);
                    }
                }
            }

            lock (_lock)
            {
                _scheduledOrders.Clear();
                _scheduledOrders.AddRange(allOrders);

                // Init NextExecutionTime if null
                foreach (var order in _scheduledOrders.Where(o => o.IsActive && o.NextExecutionTime == null))
                {
                    order.NextExecutionTime = CalculateNextExecutionTime(order);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scheduled orders from Redis");
        }
    }

    private async Task SaveToRedisAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            Dictionary<string, List<ScheduledOrder>> grouped;

            lock (_lock)
            {
                grouped = _scheduledOrders.GroupBy(o => o.AccountAlias)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            foreach (var kvp in grouped)
            {
                var key = $"scheduled:{kvp.Key}";
                var json = JsonSerializer.Serialize(kvp.Value, _jsonOptions);
                await db.StringSetAsync(key, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scheduled orders to Redis");
        }
    }

    public IEnumerable<ScheduledOrder> GetScheduledOrders()
    {
        lock (_lock)
        {
            return _scheduledOrders.ToList();
        }
    }

    public void AddOrUpdateScheduledOrder(ScheduledOrder order)
    {
        lock (_lock)
        {
            var existing = _scheduledOrders.FirstOrDefault(o => o.Id == order.Id);
            if (existing != null)
            {
                _scheduledOrders.Remove(existing);
            }

            // Recalculate next run time on update
            if (order.IsActive)
            {
                order.NextExecutionTime = CalculateNextExecutionTime(order);
            }
            else
            {
                order.NextExecutionTime = null;
            }

            _scheduledOrders.Add(order);
        }
        // Fire and forget save
        _ = SaveToRedisAsync();
        NotifyStateChanged();
    }

    public void RemoveScheduledOrder(Guid id)
    {
        lock (_lock)
        {
            var order = _scheduledOrders.FirstOrDefault(o => o.Id == id);
            if (order != null)
            {
                _scheduledOrders.Remove(order);
            }
        }
        _ = SaveToRedisAsync();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public string ExportOrders(string accountAlias)
    {
        lock (_lock)
        {
            // Filter orders for the selected account
            var ordersToExport = _scheduledOrders
                .Where(o => o.AccountAlias == accountAlias)
                .Select(o => new
                {
                    // Exclude Id and AccountAlias
                    o.Ticker,
                    o.Exchange,
                    o.Currency,
                    o.Action,
                    o.Quantity,
                    o.DaysOfWeek,
                    o.TimeMode,
                    o.TimeConfig,
                    o.IsActive,
                    o.Notes
                })
                .ToList();

            return JsonSerializer.Serialize(ordersToExport, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    public void ImportOrders(string json, string accountAlias)
    {
        try
        {
            // Define a DTO for import that matches export format (without Id and AccountAlias)
            var importedOrders = JsonSerializer.Deserialize<List<ScheduledOrderImportDto>>(json, _jsonOptions);
            if (importedOrders == null) return;

            lock (_lock)
            {
                foreach (var dto in importedOrders)
                {
                    // Use Ticker + Action + DaysOfWeek as composite key
                    var existing = _scheduledOrders.FirstOrDefault(o =>
                        o.AccountAlias == accountAlias &&
                        o.Ticker == dto.Ticker &&
                        o.Action == dto.Action &&
                        o.DaysOfWeek.SetEquals(dto.DaysOfWeek));

                    if (existing != null)
                    {
                        // Update existing order
                        existing.Exchange = dto.Exchange;
                        existing.Currency = dto.Currency;
                        existing.Quantity = dto.Quantity;
                        existing.TimeMode = dto.TimeMode;
                        existing.TimeConfig = dto.TimeConfig;
                        existing.IsActive = dto.IsActive;
                        existing.Notes = dto.Notes;

                        // Recalc execution time
                        if (existing.IsActive)
                            existing.NextExecutionTime = CalculateNextExecutionTime(existing);
                        else
                            existing.NextExecutionTime = null;
                    }
                    else
                    {
                        // Create new order
                        var newOrder = new ScheduledOrder
                        {
                            Id = Guid.NewGuid(),
                            AccountAlias = accountAlias,
                            Ticker = dto.Ticker,
                            Exchange = dto.Exchange,
                            Currency = dto.Currency,
                            Action = dto.Action,
                            Quantity = dto.Quantity,
                            DaysOfWeek = new HashSet<DayOfWeek>(dto.DaysOfWeek),
                            TimeMode = dto.TimeMode,
                            TimeConfig = dto.TimeConfig,
                            IsActive = dto.IsActive,
                            Notes = dto.Notes
                        };

                        // Recalc execution time
                        if (newOrder.IsActive)
                            newOrder.NextExecutionTime = CalculateNextExecutionTime(newOrder);

                        _scheduledOrders.Add(newOrder);
                    }
                }
            }
            _ = SaveToRedisAsync();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import orders");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SchedulerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled orders");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessScheduledOrdersAsync()
    {
        List<ScheduledOrder> ordersToExecute;
        bool stateChanged = false;

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            ordersToExecute = _scheduledOrders
                .Where(o => o.IsActive && o.NextExecutionTime != null && o.NextExecutionTime <= now)
                .ToList();
        }

        foreach (var order in ordersToExecute)
        {
            _logger.LogInformation("Executing scheduled order: {Ticker} ({Exchange})", order.Ticker, order.Exchange);

            var coreOrder = new yQuant.Core.Models.Order
            {
                AccountAlias = order.AccountAlias,
                Ticker = order.Ticker,
                Exchange = order.Exchange,
                Currency = order.Currency,
                Action = order.Action,
                Qty = order.Quantity ?? 0,
                Type = OrderType.Market
            };

            // Basic validation for Qty
            if (coreOrder.Qty <= 0)
            {
                _logger.LogWarning("Skipping order {Id} due to zero quantity", order.Id);
                // Also update next execution time to avoid infinite loop of warnings
                lock (_lock)
                {
                    order.NextExecutionTime = CalculateNextExecutionTime(order);
                }
                stateChanged = true;
                continue;
            }

            try
            {
                await _orderPublisher.PublishOrderAsync(coreOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scheduled order {Id}", order.Id);
            }

            lock (_lock)
            {
                order.LastExecutedTime = DateTime.UtcNow;
                // Calculate next run
                order.NextExecutionTime = CalculateNextExecutionTime(order);
            }
            stateChanged = true;
        }

        if (stateChanged)
        {
            await SaveToRedisAsync();
            NotifyStateChanged();
        }
    }

    public DateTime? CalculateNextExecutionTime(ScheduledOrder order)
    {
        if (order.DaysOfWeek == null || !order.DaysOfWeek.Any())
        {
            _logger.LogWarning("ScheduledOrder {Id} has no DaysOfWeek configured.", order.Id);
            return null; // Cannot run
        }

        var now = DateTime.UtcNow;
        var today = now.Date;

        // Check next 14 days to be safe
        for (int i = 0; i < 14; i++)
        {
            var checkDate = today.AddDays(i);

            // 1. Check Day of Week
            if (order.DaysOfWeek.Contains(checkDate.DayOfWeek))
            {
                // 2. Check Time
                var executionTime = GetTimeForDate(order, checkDate);

                // If it's today, it must be in the future.
                // If it's a future date, time doesn't matter (it's definitely > now)
                if (executionTime > now)
                {
                    return executionTime;
                }
            }
        }

        return null; // Should ideally not fail if DaysOfWeek is not empty, unless weird logic
    }

    private DateTime GetTimeForDate(ScheduledOrder order, DateTime date)
    {
        if (order.TimeMode == ScheduleTimeMode.FixedTime)
        {
            return date.Add(order.TimeConfig); // TimeConfig is like "14:00"
        }
        else
        {
            // Market Offset
            var marketOpen = GetMarketOpenTimeUTC(order.Exchange);
            // Combine date with market open time component
            var marketOpenDateTime = date.Add(marketOpen.TimeOfDay);
            return marketOpenDateTime.Add(order.TimeConfig); // Offset e.g. +01:00
        }
    }

    private DateTime GetMarketOpenTimeUTC(ExchangeCode exchange)
    {
        // Simple Approximation for MVP
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
