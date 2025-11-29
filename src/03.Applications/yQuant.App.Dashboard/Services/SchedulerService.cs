using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using yQuant.Core.Models;
using yQuant.App.Dashboard.Models; // For ScheduledOrder
using System.Collections.Concurrent;
using StackExchange.Redis; // For getting current price

namespace yQuant.App.Dashboard.Services;

public class SchedulerService : IHostedService, IDisposable
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OrderPublisher _orderPublisher;
    private readonly IConnectionMultiplexer _redis; // To get current prices

    private ConcurrentDictionary<Guid, ScheduledOrder> _scheduledOrders = new();
    private const string ScheduleFilePath = "schedules.json";
    private Timer? _timer;
    private TimeSpan _checkInterval = TimeSpan.FromSeconds(10); // Check every 10 seconds

    public event Action? OnChange; // Notify UI of changes

    public SchedulerService(ILogger<SchedulerService> logger, IConfiguration configuration, OrderPublisher orderPublisher, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _configuration = configuration;
        _orderPublisher = orderPublisher;
        _redis = redis;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerService is starting.");
        LoadSchedulesFromFile();
        _timer = new Timer(DoWork, null, TimeSpan.Zero, _checkInterval);
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        _logger.LogDebug("Checking scheduled orders...");
        var now = DateTime.UtcNow;
        var ordersToExecute = new List<ScheduledOrder>();

        foreach (var entry in _scheduledOrders)
        {
            var order = entry.Value;
            if (order.IsActive && order.ScheduledTime <= now && (order.LastExecutedTime == null || order.LastExecutedTime < order.ScheduledTime))
            {
                ordersToExecute.Add(order);
            }
        }

        foreach (var order in ordersToExecute)
        {
            await ExecuteScheduledOrder(order);
        }
    }

    private async Task ExecuteScheduledOrder(ScheduledOrder scheduledOrder)
    {
        _logger.LogInformation("Executing scheduled order {OrderId} for {Ticker}.", scheduledOrder.Id, scheduledOrder.Ticker);

        try
        {
            // Get current price from Redis cache
            var db = _redis.GetDatabase();
            // Assuming current price is stored as cache:ticker:{TICKER}:price
            var priceKey = $"cache:ticker:{scheduledOrder.Ticker}:price"; // This key might need to be standardized
            var currentPriceString = await db.StringGetAsync(priceKey);
            decimal currentPrice = 0;
            if (currentPriceString.HasValue)
            {
                currentPrice = decimal.Parse(currentPriceString.ToString());
            }

            if (currentPrice <= 0)
            {
                _logger.LogWarning("Cannot execute scheduled order {OrderId}. Current price for {Ticker} is zero or not found.", scheduledOrder.Id, scheduledOrder.Ticker);
                return;
            }

            int finalQty;
            if (scheduledOrder.Quantity.HasValue && scheduledOrder.Quantity > 0)
            {
                finalQty = scheduledOrder.Quantity.Value;
            }
            else
            {
                finalQty = (int)Math.Floor(scheduledOrder.TargetAmount / currentPrice);
            }

            if (finalQty <= 0)
            {
                _logger.LogWarning("Calculated quantity for scheduled order {OrderId} is zero. Not executing.", scheduledOrder.Id);
                return;
            }

            var order = new yQuant.Core.Models.Order
            {
                AccountAlias = scheduledOrder.AccountAlias,
                Ticker = scheduledOrder.Ticker,
                Action = scheduledOrder.Action,
                Type = OrderType.Market, // Scheduled orders are market orders
                Qty = finalQty,
                Price = currentPrice, // Price at execution time
                Timestamp = DateTime.UtcNow
            };

            await _orderPublisher.PublishOrderAsync(order);

            scheduledOrder.LastExecutedTime = DateTime.UtcNow;
            if (!scheduledOrder.IsRecurring)
            {
                scheduledOrder.IsActive = false; // Deactivate one-time orders
            }
            // For recurring orders, update ScheduledTime based on RecurrencePattern
            // This is a placeholder for complex recurrence logic.

            SaveSchedulesToFile(); // Save changes
            OnChange?.Invoke(); // Notify UI
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled order {OrderId} for {Ticker}.", scheduledOrder.Id, scheduledOrder.Ticker);
        }
    }

    private void LoadSchedulesFromFile()
    {
        if (File.Exists(ScheduleFilePath))
        {
            try
            {
                var json = File.ReadAllText(ScheduleFilePath);
                var orders = JsonSerializer.Deserialize<List<ScheduledOrder>>(json);
                if (orders != null)
                {
                    _scheduledOrders = new ConcurrentDictionary<Guid, ScheduledOrder>(orders.ToDictionary(o => o.Id));
                    _logger.LogInformation("Loaded {Count} scheduled orders from file.", _scheduledOrders.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load schedules from file.");
            }
        }
    }

    private void SaveSchedulesToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_scheduledOrders.Values.ToList());
            File.WriteAllText(ScheduleFilePath, json);
            _logger.LogInformation("Saved {Count} scheduled orders to file.", _scheduledOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save schedules to file.");
        }
    }

    public IEnumerable<ScheduledOrder> GetScheduledOrders() => _scheduledOrders.Values;

    public void AddOrUpdateScheduledOrder(ScheduledOrder order)
    {
        _scheduledOrders.AddOrUpdate(order.Id, order, (key, existingVal) => order);
        SaveSchedulesToFile();
        OnChange?.Invoke();
    }

    public void RemoveScheduledOrder(Guid id)
    {
        _scheduledOrders.TryRemove(id, out _);
        SaveSchedulesToFile();
        OnChange?.Invoke();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerService is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
