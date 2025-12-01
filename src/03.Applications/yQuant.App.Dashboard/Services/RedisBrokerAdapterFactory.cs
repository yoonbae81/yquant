using StackExchange.Redis;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Adapters;

namespace yQuant.App.Dashboard.Services;

public class RedisBrokerAdapterFactory : IBrokerAdapterFactory
{
    private readonly IConnectionMultiplexer _redis;

    public RedisBrokerAdapterFactory(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public IBrokerAdapter? GetAdapter(string alias)
    {
        return new RedisBrokerClient(_redis, alias);
    }

    public IEnumerable<string> GetAvailableAccounts()
    {
        // In a real scenario, we might fetch this from Redis or Config
        return Enumerable.Empty<string>();
    }
}
