using StackExchange.Redis;
using yQuant.Core.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace yQuant.App.Dashboard.Services;

public class OrderPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OrderPublisher> _logger;

    public OrderPublisher(IConnectionMultiplexer redis, ILogger<OrderPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task PublishOrderAsync(yQuant.Core.Models.Order order)
    {
        try
        {
            var db = _redis.GetDatabase();
            var orderJson = JsonSerializer.Serialize(order);
            await db.PublishAsync(RedisChannel.Literal("order"), orderJson);
            _logger.LogInformation("Order {OrderId} published to Redis 'order' channel.", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish order {OrderId} to Redis 'order' channel.", order.Id);
            throw; // Re-throw to inform caller about the failure
        }
    }
}
