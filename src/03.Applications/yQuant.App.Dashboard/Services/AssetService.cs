using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace yQuant.App.Dashboard.Services;

public class AssetService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IRedisService _redisService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssetService> _logger;
    private readonly TimeSpan _cacheDuration;

    public AssetService(
        IConnectionMultiplexer redis,
        IRedisService redisService,
        IConfiguration configuration,
        ILogger<AssetService> logger)
    {
        _redis = redis;
        _redisService = redisService;
        _configuration = configuration;
        _logger = logger;

        var minutes = _configuration.GetValue<int>("CacheSettings:AssetCacheDurationMinutes", 1);
        _cacheDuration = TimeSpan.FromMinutes(minutes);
    }

    public virtual async Task<List<string>> GetAvailableAccountsAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = await db.StringGetAsync("broker:accounts");
            if (json.HasValue)
            {
                var accounts = JsonSerializer.Deserialize<List<string>>(json.ToString()) ?? [];
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Fetched {Count} accounts from Redis: {Accounts}", accounts.Count, string.Join(", ", accounts));
                }
                return accounts;
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to fetch available accounts from Redis");
            }
        }
        return [];
    }

    public static Account GetAccountBasicInfo(string alias)
    {
        // Return a basic account object with just the alias and broker name
        // This avoids a remote call if we just need the name for a dropdown
        return new Account
        {
            Alias = alias,
            Broker = "Redis", // Default for Redis-connected accounts
            Number = "N/A",
            AppKey = "N/A",
            AppSecret = "N/A",
            Deposits = [],
            Active = true
        };
    }

    public virtual async Task<Account?> GetAccountOverviewAsync(string accountAlias)
    {
        var cacheKey = $"asset:account:{accountAlias}";

        // 1. Try to get from cache
        try
        {
            var cachedAccount = await _redisService.GetAsync<Account>(cacheKey);
            if (cachedAccount != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Cache hit for account {Alias}", accountAlias);
                }
                return cachedAccount;
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to retrieve account {Alias} from cache", accountAlias);
            }
        }

        // 2. Fetch from broker
        // Instantiate client directly
        var adapter = new yQuant.Infra.Redis.Adapters.RedisBrokerClient(_redis, accountAlias);

        Account account;
        try
        {
            // Get Account State (Deposits)
            account = await adapter.GetDepositAsync(null);

            // Get Positions
            var positions = await adapter.GetPositionsAsync();

            // Merge Positions into Account
            account.Positions = positions;
            account.Alias = accountAlias; // Ensure alias is set
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to fetch account data for {Alias} from broker", accountAlias);
            }
            throw; // Or return null depending on error handling strategy
        }

        // 3. Save to cache
        try
        {
            await _redisService.SetAsync(cacheKey, account, _cacheDuration);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Cached account {Alias} for {Duration} minutes", accountAlias, _cacheDuration.TotalMinutes);
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to cache account {Alias}", accountAlias);
            }
        }

        return account;
    }
}
