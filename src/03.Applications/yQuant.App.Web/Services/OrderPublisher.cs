using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Redis.Models;

namespace yQuant.App.Web.Services;

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

        var db = _redis.GetDatabase();
        var orderJson = JsonSerializer.Serialize(order);
        await db.PublishAsync(RedisChannel.Literal("order"), orderJson);

        _logger.LogInformation("Order published to 'order' channel");
    }
}
