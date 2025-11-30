using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using yQuant.App.Dashboard.Models;
using yQuant.Core.Models;

namespace yQuant.App.Dashboard.Services;

public class SchedulerService : BackgroundService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly OrderPublisher _orderPublisher;
    private readonly List<ScheduledOrder> _scheduledOrders = new();
    private readonly object _lock = new();

    public event Action? OnChange;

    public SchedulerService(
        ILogger<SchedulerService> logger,
        OrderPublisher orderPublisher)
    {
        _logger = logger;
        _orderPublisher = orderPublisher;
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
            
            // If it's a new order (Id might be empty if not set by caller, but usually caller sets it or we set it here)
            if (order.Id == Guid.Empty)
            {
                order.Id = Guid.NewGuid();
            }

            _scheduledOrders.Add(order);
        }
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
            
            // Create core Order object
            var coreOrder = new Order
            {
                AccountAlias = order.AccountAlias,
                Ticker = order.Ticker,
                Action = order.Action,
                Qty = order.Quantity ?? 0, // Handle optional quantity
                // Amount = order.TargetAmount // Order model might need Amount if Qty is missing
                Type = OrderType.Market // Default to market for now
            };

            // Fire and forget execution to not block scheduler? 
            // Or await? Better to await to ensure it's done.
            try 
            {
                // We need to run this in a way that doesn't block the loop too long, 
                // but for now simple await is fine.
                 _orderPublisher.PublishOrderAsync(coreOrder).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                 _logger.LogError(ex, "Failed to execute scheduled order {Id}", order.Id);
            }

            // Handle recurrence or remove
            lock (_lock)
            {
                if (order.IsRecurring)
                {
                    // Update next run time
                    if (order.RecurrencePattern == "Daily")
                        order.ScheduledTime = order.ScheduledTime.AddDays(1);
                    else if (order.RecurrencePattern == "Weekly")
                        order.ScheduledTime = order.ScheduledTime.AddDays(7);
                }
                else
                {
                    order.IsActive = false; // Mark as done
                    // Or remove?
                    // _scheduledOrders.Remove(order);
                }
            }
            NotifyStateChanged();
        }
    }
}
