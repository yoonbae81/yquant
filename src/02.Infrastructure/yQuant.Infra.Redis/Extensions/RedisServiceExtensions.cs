using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Infra.Redis.Services;

namespace yQuant.Infra.Redis.Extensions;

public static class RedisServiceExtensions
{
    public static IServiceCollection AddRedisMiddleware(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Messaging Redis (for Pub/Sub, Heartbeats, Trades, and general app state)
        var msgConnectionString = configuration["Redis:Message"];

        if (string.IsNullOrEmpty(msgConnectionString))
        {
            throw new InvalidOperationException("Redis Messaging connection string (Redis:Message) is missing. Please check appsecrets.json.");
        }

        var options = ConfigurationOptions.Parse(msgConnectionString);
        options.AbortOnConnectFail = false;
        var multiplexer = ConnectionMultiplexer.Connect(options);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<IRedisService, RedisService>();

        // 2. Token Redis (Shared across environments specifically for KIS access tokens)
        var tokenConnectionString = configuration["Redis:Token"];

        if (string.IsNullOrEmpty(tokenConnectionString))
        {
            throw new InvalidOperationException("Redis Token connection string (Redis:Token) is missing. Please check appsecrets.json.");
        }

        var tokenOptions = ConfigurationOptions.Parse(tokenConnectionString);
        tokenOptions.AbortOnConnectFail = false;
        var tokenMultiplexer = ConnectionMultiplexer.Connect(tokenOptions);

        services.AddSingleton<ITokenRedisService>(sp =>
            new TokenRedisService(tokenMultiplexer, sp.GetRequiredService<ILogger<TokenRedisService>>()));

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
