using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.Infra.Redis.Services;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisService> _logger;
    private readonly IDatabase _database;

    public RedisService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
        _database = _connectionMultiplexer.GetDatabase();
    }

    public IConnectionMultiplexer Connection => _connectionMultiplexer;

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from Redis for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis for key: {Key}", key);
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            return await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key from Redis: {Key}", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence in Redis for key: {Key}", key);
            return false;
        }
    }
}
