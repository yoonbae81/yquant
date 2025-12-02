using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using yQuant.App.Dashboard.Models;
using yQuant.Core.Models;

namespace yQuant.App.Dashboard.Services;

public class SchedulerService : BackgroundService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly OrderPublisher _orderPublisher;
    private readonly IConnectionMultiplexer _redis;
    private readonly List<ScheduledOrder> _scheduledOrders = new();
    private readonly object _lock = new();
    private const string RedisKey = "dashboard:scheduled_orders";

    public event Action? OnChange;

    public SchedulerService(
        ILogger<SchedulerService> logger,
        OrderPublisher orderPublisher,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _orderPublisher = orderPublisher;
        _redis = redis;
        LoadFromRedis();
    }

    private void LoadFromRedis()
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = db.StringGet(RedisKey);
            if (json.HasValue)
            {
                var orders = JsonSerializer.Deserialize<List<ScheduledOrder>>(json.ToString());
                if (orders != null)
                {
                    lock (_lock)
                    {
                        _scheduledOrders.Clear();
                        _scheduledOrders.AddRange(orders);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scheduled orders from Redis");
        }
    }

    private void SaveToRedis()
    {
        try
        {
            var db = _redis.GetDatabase();
            string json;
            lock (_lock)
            {
                json = JsonSerializer.Serialize(_scheduledOrders);
            }
            db.StringSet(RedisKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scheduled orders to Redis");
        }
    }

    public string ExportOrders()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_scheduledOrders);
        }
    }

    public void ImportOrders(string json)
    {
        try
        {
            var importedOrders = JsonSerializer.Deserialize<List<ScheduledOrder>>(json);
            if (importedOrders == null) return;

            lock (_lock)
            {
                foreach (var order in importedOrders)
                {
                    // Overwrite if AccountAlias and Ticker match
                    var existing = _scheduledOrders.Where(o => o.AccountAlias == order.AccountAlias && o.Ticker == order.Ticker).ToList();
                    foreach (var item in existing)
                    {
                        _scheduledOrders.Remove(item);
                    }

                    // Ensure ID is set if missing (though import should have it)
                    if (order.Id == Guid.Empty) order.Id = Guid.NewGuid();

                    _scheduledOrders.Add(order);
                }
            }
            SaveToRedis();
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import orders");
            throw;
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

            if (order.Id == Guid.Empty)
            {
                order.Id = Guid.NewGuid();
            }

            _scheduledOrders.Add(order);
        }
        SaveToRedis();
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
        SaveToRedis();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SchedulerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ProcessScheduledOrders();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled orders");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private void ProcessScheduledOrders()
    {
        List<ScheduledOrder> ordersToExecute;
        bool stateChanged = false;

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            ordersToExecute = _scheduledOrders
                .Where(o => o.IsActive && o.ScheduledTime <= now)
                .ToList();
        }

        foreach (var order in ordersToExecute)
        {
            _logger.LogInformation("Executing scheduled order: {Ticker}", order.Ticker);

            var coreOrder = new yQuant.Core.Models.Order
            {
                AccountAlias = order.AccountAlias,
                Ticker = order.Ticker,
                Action = order.Action,
                Qty = order.Quantity ?? 0,
                Type = OrderType.Market
            };

            try
            {
                _orderPublisher.PublishOrderAsync(coreOrder).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scheduled order {Id}", order.Id);
            }

            lock (_lock)
            {
                if (order.IsRecurring)
                {
                    if (order.RecurrencePattern == "Daily")
                        order.ScheduledTime = order.ScheduledTime.AddDays(1);
                    else if (order.RecurrencePattern == "Weekly")
                        order.ScheduledTime = order.ScheduledTime.AddDays(7);
                }
                else
                {
                    order.IsActive = false;
                }
                order.LastExecutedTime = DateTime.UtcNow;
            }
            stateChanged = true;
        }

        if (stateChanged)
        {
            SaveToRedis();
            NotifyStateChanged();
        }
    }
}
