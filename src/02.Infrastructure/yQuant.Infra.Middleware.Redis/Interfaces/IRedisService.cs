using StackExchange.Redis;

namespace yQuant.Infra.Middleware.Redis.Interfaces;

public interface IRedisService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    
    /// <summary>
    /// Gets the underlying Redis connection multiplexer for advanced operations.
    /// </summary>
    IConnectionMultiplexer Connection { get; }
}
