using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Dashboard.Services;

public class RedisBrokerAdapterFactory : IBrokerAdapterFactory
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;

    public RedisBrokerAdapterFactory(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis;
        _configuration = configuration;
    }

    public IEnumerable<string> GetAvailableAccounts()
    {
        var accountsSection = _configuration.GetSection("Accounts");
        foreach (var section in accountsSection.GetChildren())
        {
            var alias = section["Alias"];
            if (!string.IsNullOrEmpty(alias))
            {
                yield return alias;
            }
        }
    }

    public IBrokerAdapter? GetAdapter(string alias)
    {
        // We assume all accounts are accessible via Redis
        return new RedisBrokerClient(_redis, alias);
    }
}
