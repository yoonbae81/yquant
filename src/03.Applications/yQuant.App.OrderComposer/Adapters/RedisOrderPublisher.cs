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

        var request = new yQuant.Infra.Redis.Models.BrokerRequest
        {
            Id = Guid.NewGuid(),
            Type = yQuant.Infra.Redis.Models.BrokerRequestType.PlaceOrder,
            Account = order.AccountAlias,
            Payload = JsonSerializer.Serialize(order),
            ResponseChannel = string.Empty // Fire and forget for now, or we could listen for confirmation
        };

        var requestJson = JsonSerializer.Serialize(request);
        await db.PublishAsync(RedisChannel.Literal("broker:requests"), requestJson);
    }
}
