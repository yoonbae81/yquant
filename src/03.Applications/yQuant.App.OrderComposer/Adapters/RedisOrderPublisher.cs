using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Ports.Output.Infrastructure;
using CoreOrder = yQuant.Core.Models.Order;

namespace yQuant.App.OrderComposer.Adapters;

public class RedisOrderPublisher : IOrderPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public RedisOrderPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishOrderAsync(CoreOrder order)
    {
        var db = _redis.GetDatabase();
        var orderJson = JsonSerializer.Serialize(order);
        await db.PublishAsync(RedisChannel.Literal("order"), orderJson);
    }
}
