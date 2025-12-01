using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Dashboard.Services;

public class OrderPublisher
{
    private readonly ILogger<OrderPublisher> _logger;
    private readonly IBrokerAdapterFactory _adapterFactory;

    public OrderPublisher(
        ILogger<OrderPublisher> logger,
        IBrokerAdapterFactory adapterFactory)
    {
        _logger = logger;
        _adapterFactory = adapterFactory;
    }

    public async Task PublishOrderAsync(Order order)
    {
        _logger.LogInformation("Publishing order: {Ticker} {Action} {Qty}", order.Ticker, order.Action, order.Qty);

        var adapter = _adapterFactory.GetAdapter(order.AccountAlias);
        if (adapter == null)
        {
            throw new InvalidOperationException($"Broker adapter not found for account: {order.AccountAlias}");
        }

        // Execute order directly for now
        // In a real event-driven system, this might publish to a bus
        await adapter.PlaceOrderAsync(order);
        
        _logger.LogInformation("Order executed successfully.");
    }
}
