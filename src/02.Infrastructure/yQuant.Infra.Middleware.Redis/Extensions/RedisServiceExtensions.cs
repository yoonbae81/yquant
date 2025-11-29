using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using yQuant.Infra.Middleware.Redis.Interfaces;
using yQuant.Infra.Middleware.Redis.Services;

namespace yQuant.Infra.Middleware.Redis.Extensions;

public static class RedisServiceExtensions
{
    public static IServiceCollection AddRedisMiddleware(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis") ?? configuration["Redis:ConnectionString"];
        
        if (string.IsNullOrEmpty(connectionString))
        {
            // If no Redis config, we might want to warn or throw. 
            // For now, let's assume it's optional or handled elsewhere, 
            // but strictly speaking, if this is called, we expect Redis to be needed.
            throw new InvalidOperationException("Redis connection string is not configured.");
        }

        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<IRedisService, RedisService>();

        return services;
    }
}
