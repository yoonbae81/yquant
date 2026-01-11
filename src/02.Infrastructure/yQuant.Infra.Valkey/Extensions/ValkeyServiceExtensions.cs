using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Valkey.Services;

namespace yQuant.Infra.Valkey.Extensions;

public static class ValkeyServiceExtensions
{
    public static IServiceCollection AddValkeyMiddleware(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Messaging Valkey (for Pub/Sub, Heartbeats, Trades, and general app state)
        var msgConnectionString = configuration.GetConnectionString("Valkey");

        if (string.IsNullOrEmpty(msgConnectionString))
        {
            throw new InvalidOperationException("Valkey connection string (ConnectionStrings:Valkey) is missing. Please check appsecrets.json.");
        }

        var options = ConfigurationOptions.Parse(msgConnectionString);
        options.AbortOnConnectFail = false;
        var multiplexer = ConnectionMultiplexer.Connect(options);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<IValkeyService, ValkeyService>();

        // 2. Subscription Services (For memory cache reloads via Pub/Sub)
        services.AddHostedService<CatalogUpdateSubscriber>();

        return services;
    }
    public static IServiceCollection AddHeartbeat(this IServiceCollection services, string serviceName)
    {
        services.AddHostedService(sp => new HeartbeatService(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<ILogger<HeartbeatService>>(),
            serviceName));
        return services;
    }
}
