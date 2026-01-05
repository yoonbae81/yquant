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
        var msgConnectionString = configuration["Valkey:Message"];

        if (string.IsNullOrEmpty(msgConnectionString))
        {
            throw new InvalidOperationException("Valkey Messaging connection string (Valkey:Message) is missing. Please check appsecrets.json.");
        }

        var options = ConfigurationOptions.Parse(msgConnectionString);
        options.AbortOnConnectFail = false;
        var multiplexer = ConnectionMultiplexer.Connect(options);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<IValkeyService, ValkeyService>();

        // 2. Storage Valkey (Shared across environments for KIS access tokens, Scheduled Orders, and Stock Catalog)
        var storageConnectionString = configuration["Valkey:Storage"];

        if (string.IsNullOrEmpty(storageConnectionString))
        {
            throw new InvalidOperationException("Valkey Storage connection string (Valkey:Storage) is missing. Please check appsecrets.json.");
        }

        var storageOptions = ConfigurationOptions.Parse(storageConnectionString);
        storageOptions.AbortOnConnectFail = false;
        var storageMultiplexer = ConnectionMultiplexer.Connect(storageOptions);

        services.AddSingleton<IStorageValkeyService>(sp =>
            new StorageValkeyService(storageMultiplexer, sp.GetRequiredService<ILogger<StorageValkeyService>>()));

        // 3. Stock Catalog Services (In-memory cache with Pub/Sub reload)
        services.AddSingleton<StockCatalogRepository>();
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
