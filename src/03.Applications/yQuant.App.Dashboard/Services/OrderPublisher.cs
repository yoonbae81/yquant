using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Redis.Models;

namespace yQuant.App.Dashboard.Services;

public class OrderPublisher
{
    private readonly ILogger<OrderPublisher> _logger;
    private readonly IConnectionMultiplexer _redis;

    public OrderPublisher(
        ILogger<OrderPublisher> logger,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task PublishOrderAsync(yQuant.Core.Models.Order order)
    {
        _logger.LogInformation("Publishing order to Redis: {Ticker} {Action} {Qty}", order.Ticker, order.Action, order.Qty);

        var request = new BrokerRequest
        {
            Id = Guid.NewGuid(),
            Type = BrokerRequestType.PlaceOrder,
            Account = order.AccountAlias,
            Payload = JsonSerializer.Serialize(order),
            ResponseChannel = string.Empty // Fire and forget
        };

        var db = _redis.GetDatabase();
        await db.PublishAsync(RedisChannel.Literal("broker:requests"), JsonSerializer.Serialize(request));

        _logger.LogInformation("Order published to broker:requests");
    }
}
