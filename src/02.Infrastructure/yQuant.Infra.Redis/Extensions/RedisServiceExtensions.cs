using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Infra.Redis.Services;

namespace yQuant.Infra.Redis.Extensions;

public static class RedisServiceExtensions
{
    public static IServiceCollection AddRedisMiddleware(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Redis"];

        if (string.IsNullOrEmpty(connectionString))
        {
            // If no Redis config, we might want to warn or throw. 
            // For now, let's assume it's optional or handled elsewhere, 
            // but strictly speaking, if this is called, we expect Redis to be needed.
            throw new InvalidOperationException("Redis connection string is not configured.");
        }

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<IRedisService, RedisService>();

        return services;
    }
}
