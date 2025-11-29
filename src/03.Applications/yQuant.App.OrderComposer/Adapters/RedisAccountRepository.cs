using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.OrderComposer.Adapters;

public class RedisAccountRepository : IAccountRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisAccountRepository> _logger;

    public RedisAccountRepository(IConnectionMultiplexer redis, ILogger<RedisAccountRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<Account?> GetAccountAsync(string accountAlias)
    {
        var db = _redis.GetDatabase();
        var accountCacheKey = $"cache:account:{accountAlias}";
        var accountJson = await db.StringGetAsync(accountCacheKey);

        if (!accountJson.HasValue)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Account>(accountJson.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize account from cache for {AccountAlias}", accountAlias);
            return null;
        }
    }
}
