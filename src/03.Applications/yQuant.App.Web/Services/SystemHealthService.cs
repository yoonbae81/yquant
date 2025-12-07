using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace yQuant.App.Web.Services;

public class SystemHealthService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SystemHealthService> _logger;

    public SystemHealthService(
        IConnectionMultiplexer redis,
        ILogger<SystemHealthService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> CheckRedisAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckServiceAsync(string serviceName)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"status:heartbeat:{serviceName}";
            return await db.KeyExistsAsync(key);
        }
        catch
        {
            return false;
        }
    }
}
