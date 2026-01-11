using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using yQuant.App.Dashboard.Models;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Dashboard.Services;

/// <summary>
/// Manages CRUD operations for scheduled orders and liquidations.
/// Actual execution is handled by OrderManager.
/// </summary>
public class SchedulerService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IScheduledOrderRepository _scheduledOrderRepository;

    // ScheduledLiquidation cache
    private readonly List<ScheduledLiquidation> _scheduledLiquidations = new();
    private readonly object _lock = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Event raised when internal state changes (for UI update)
    public event Action? OnChange;

    public SchedulerService(
        ILogger<SchedulerService> logger,
        IConnectionMultiplexer redis,
        IScheduledOrderRepository scheduledOrderRepository)
    {
        _logger = logger;
        _redis = redis;
        _scheduledOrderRepository = scheduledOrderRepository;

        // Load Liquidations on construction
        _ = LoadLiquidationsFromValkeyAsync();
    }

    private async Task LoadLiquidationsFromValkeyAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var accounts = await db.SetMembersAsync("account:index");
            var allLiquidations = new List<ScheduledLiquidation>();

            foreach (var account in accounts)
            {
                var liquidationKey = $"scheduled_liquidation:{account}";
                var json = await db.StringGetAsync(liquidationKey);
                if (json.HasValue)
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<ScheduledLiquidation>>(json.ToString(), _jsonOptions);
                        if (list != null) allLiquidations.AddRange(list);
                    }
                    catch
                    {
                        _logger.LogWarning("Failed to deserialize liquidations for {Account}", account);
                    }
                }
            }

            lock (_lock)
            {
                _scheduledLiquidations.Clear();
                _scheduledLiquidations.AddRange(allLiquidations);

                foreach (var l in _scheduledLiquidations.Where(l => l.IsActive && l.NextExecutionTime == null))
                {
                    l.NextExecutionTime = CalculateNextExecutionTimeLiquidation(l);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load liquidations");
        }
    }

    // ========== Scheduled Order Methods (Async via Repository) ==========

    public async Task<IEnumerable<ScheduledOrder>> GetScheduledOrdersAsync(string accountAlias)
    {
        return await _scheduledOrderRepository.GetAllAsync(accountAlias);
    }

    public async Task AddOrUpdateScheduledOrderAsync(ScheduledOrder order)
    {
        // Calculate Time
        if (order.IsActive)
            order.NextExecutionTime = CalculateNextExecutionTime(order);
        else
            order.NextExecutionTime = null;

        await _scheduledOrderRepository.AddOrUpdateAsync(order);
        NotifyStateChanged();
    }

    public async Task RemoveScheduledOrderAsync(string accountAlias, Guid id)
    {
        await _scheduledOrderRepository.RemoveAsync(accountAlias, id);
        NotifyStateChanged();
    }

    public async Task<string> ExportOrdersAsync(string accountAlias)
    {
        var orders = await _scheduledOrderRepository.GetAllAsync(accountAlias);
        var exportList = orders.Select(o => new ScheduledOrderImportDto
        {
            Ticker = o.Ticker,
            Exchange = o.Exchange,
            Currency = o.Currency,
            Action = o.Action,
            Quantity = o.Quantity,
            DaysOfWeek = o.DaysOfWeek,
            TimeMode = o.TimeMode,
            TimeConfig = o.TimeConfig,
            IsActive = o.IsActive,
            Notes = o.Notes
        }).ToList();

        return JsonSerializer.Serialize(exportList, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    public async Task ImportOrdersAsync(string json, string accountAlias)
    {
        try
        {
            var dtos = JsonSerializer.Deserialize<List<ScheduledOrderImportDto>>(json, _jsonOptions);
            if (dtos == null) return;

            await _scheduledOrderRepository.ProcessOrdersAsync(accountAlias, async (orders) =>
            {
                foreach (var dto in dtos)
                {
                    var existing = orders.FirstOrDefault(o =>
                        o.Ticker == dto.Ticker &&
                        o.Action == dto.Action &&
                        o.DaysOfWeek.SetEquals(dto.DaysOfWeek));

                    if (existing != null)
                    {
                        existing.Exchange = dto.Exchange;
                        existing.Currency = dto.Currency;
                        existing.Quantity = dto.Quantity;
                        existing.TimeMode = dto.TimeMode;
                        existing.TimeConfig = dto.TimeConfig;
                        existing.IsActive = dto.IsActive;
                        existing.Notes = dto.Notes;

                        existing.NextExecutionTime = existing.IsActive
                            ? CalculateNextExecutionTime(existing)
                            : null;
                    }
                    else
                    {
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
                        newOrder.NextExecutionTime = newOrder.IsActive
                            ? CalculateNextExecutionTime(newOrder)
                            : null;

                        orders.Add(newOrder);
                    }
                }
                return await Task.FromResult(true);
            }, waitForLock: true);

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import orders");
            throw;
        }
    }

    // ========== Scheduled Liquidation Methods (Legacy Sync/Internal) ==========

    private async Task SaveLiquidationsToValkeyAsync()
    {
        // Simple save all based on internal cache
        try
        {
            var db = _redis.GetDatabase();
            Dictionary<string, List<ScheduledLiquidation>> grouped;
            lock (_lock)
            {
                grouped = _scheduledLiquidations.GroupBy(l => l.AccountAlias)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            var accounts = await db.SetMembersAsync("account:index");
            foreach (var acc in accounts)
            {
                var alias = acc.ToString();
                var key = $"scheduled_liquidation:{alias}";
                if (grouped.TryGetValue(alias, out var list))
                {
                    await db.StringSetAsync(key, JsonSerializer.Serialize(list, _jsonOptions));
                }
                else
                {
                    await db.KeyDeleteAsync(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save liquidations");
        }
    }

    public IEnumerable<ScheduledLiquidation> GetScheduledLiquidations(string? accountAlias = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(accountAlias))
                return _scheduledLiquidations.ToList();
            return _scheduledLiquidations.Where(l => l.AccountAlias == accountAlias).ToList();
        }
    }

    public void AddOrUpdateScheduledLiquidation(ScheduledLiquidation liquidation)
    {
        lock (_lock)
        {
            var existing = _scheduledLiquidations.FirstOrDefault(l => l.Id == liquidation.Id);
            if (existing != null) _scheduledLiquidations.Remove(existing);

            if (liquidation.IsActive)
                liquidation.NextExecutionTime = CalculateNextExecutionTimeLiquidation(liquidation);
            else
                liquidation.NextExecutionTime = null;

            _scheduledLiquidations.Add(liquidation);
        }
        _ = SaveLiquidationsToValkeyAsync();
        NotifyStateChanged();
    }

    public void RemoveScheduledLiquidation(Guid id)
    {
        lock (_lock)
        {
            var l = _scheduledLiquidations.FirstOrDefault(x => x.Id == id);
            if (l != null) _scheduledLiquidations.Remove(l);
        }
        _ = SaveLiquidationsToValkeyAsync();
        NotifyStateChanged();
    }

    public string ExportLiquidations(string accountAlias)
    {
        lock (_lock)
        {
            var list = _scheduledLiquidations.Where(l => l.AccountAlias == accountAlias).Select(l => new ScheduledLiquidationImportDto
            {
                CountryFilter = l.CountryFilter,
                BuyReasonFilter = l.BuyReasonFilter,
                DaysOfWeek = l.DaysOfWeek,
                TimeMode = l.TimeMode,
                TimeConfig = l.TimeConfig,
                IsActive = l.IsActive,
                Notes = l.Notes
            }).ToList();

            return JsonSerializer.Serialize(list, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }
    }

    public void ImportLiquidations(string json, string accountAlias)
    {
        try
        {
            var dtos = JsonSerializer.Deserialize<List<ScheduledLiquidationImportDto>>(json, _jsonOptions);
            if (dtos == null) return;

            lock (_lock)
            {
                foreach (var dto in dtos)
                {
                    var existing = _scheduledLiquidations.FirstOrDefault(l =>
                        l.AccountAlias == accountAlias &&
                        l.CountryFilter == dto.CountryFilter &&
                        l.BuyReasonFilter == dto.BuyReasonFilter &&
                        l.DaysOfWeek.SetEquals(dto.DaysOfWeek));

                    if (existing != null)
                    {
                        existing.TimeMode = dto.TimeMode;
                        existing.TimeConfig = dto.TimeConfig;
                        existing.IsActive = dto.IsActive;
                        existing.Notes = dto.Notes;

                        existing.NextExecutionTime = existing.IsActive
                            ? CalculateNextExecutionTimeLiquidation(existing)
                            : null;
                    }
                    else
                    {
                        var newL = new ScheduledLiquidation
                        {
                            Id = Guid.NewGuid(),
                            AccountAlias = accountAlias,
                            CountryFilter = dto.CountryFilter,
                            BuyReasonFilter = dto.BuyReasonFilter,
                            DaysOfWeek = new HashSet<DayOfWeek>(dto.DaysOfWeek),
                            TimeMode = dto.TimeMode,
                            TimeConfig = dto.TimeConfig,
                            IsActive = dto.IsActive,
                            Notes = dto.Notes
                        };
                        newL.NextExecutionTime = newL.IsActive
                            ? CalculateNextExecutionTimeLiquidation(newL)
                            : null;
                        _scheduledLiquidations.Add(newL);
                    }
                }
            }
            _ = SaveLiquidationsToValkeyAsync();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import liquidations");
            throw;
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public DateTime? CalculateNextExecutionTime(ScheduledOrder order)
    {
        if (order.DaysOfWeek == null || !order.DaysOfWeek.Any()) return null;

        var now = DateTime.UtcNow;
        var today = now.Date;

        for (int i = 0; i < 14; i++)
        {
            var checkDate = today.AddDays(i);
            if (order.DaysOfWeek.Contains(checkDate.DayOfWeek))
            {
                var executionTime = GetTimeForDate(order, checkDate);
                if (executionTime > now) return executionTime;
            }
        }
        return null;
    }

    private DateTime GetTimeForDate(ScheduledOrder order, DateTime date)
    {
        if (order.TimeMode == ScheduleTimeMode.FixedTime)
            return date.Add(order.TimeConfig);
        else
        {
            var marketOpen = GetMarketOpenTimeUTC(order.Exchange);
            return date.Add(marketOpen.TimeOfDay).Add(order.TimeConfig);
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

    private DateTime? CalculateNextExecutionTimeLiquidation(ScheduledLiquidation liquidation)
    {
        if (liquidation.DaysOfWeek == null || !liquidation.DaysOfWeek.Any()) return null;

        var now = DateTime.UtcNow;
        var today = now.Date;

        for (int i = 0; i < 14; i++)
        {
            var checkDate = today.AddDays(i);

            if (liquidation.DaysOfWeek.Contains(checkDate.DayOfWeek))
            {
                var executionTime = liquidation.TimeMode == ScheduleTimeMode.FixedTime
                    ? checkDate.Add(liquidation.TimeConfig)
                    : checkDate.Add(TimeSpan.FromHours(14.5)).Add(liquidation.TimeConfig);
                if (executionTime > now) return executionTime;
            }
        }
        return null;
    }
}
