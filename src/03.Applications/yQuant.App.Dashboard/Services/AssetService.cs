using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.App.Dashboard.Services;

public class AssetService
{
    private readonly IBrokerAdapterFactory _adapterFactory;
    private readonly IRedisService _redisService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssetService> _logger;
    private readonly TimeSpan _cacheDuration;

    public AssetService(
        IBrokerAdapterFactory adapterFactory,
        IRedisService redisService,
        IConfiguration configuration,
        ILogger<AssetService> logger)
    {
        _adapterFactory = adapterFactory;
        _redisService = redisService;
        _configuration = configuration;
        _logger = logger;

        var minutes = _configuration.GetValue<int>("CacheSettings:AssetCacheDurationMinutes", 1);
        _cacheDuration = TimeSpan.FromMinutes(minutes);
    }

    public async Task<Account?> GetAccountOverviewAsync(string accountAlias)
    {
        var cacheKey = $"asset:account:{accountAlias}";

        // 1. Try to get from cache
        try
        {
            var cachedAccount = await _redisService.GetAsync<Account>(cacheKey);
            if (cachedAccount != null)
            {
                _logger.LogDebug("Cache hit for account {Alias}", accountAlias);
                return cachedAccount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve account {Alias} from cache", accountAlias);
        }

        // 2. Fetch from broker
        var adapter = _adapterFactory.GetAdapter(accountAlias);
        if (adapter == null)
        {
            return null;
        }

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
            _logger.LogError(ex, "Failed to fetch account data for {Alias} from broker", accountAlias);
            throw; // Or return null depending on error handling strategy
        }

        // 3. Save to cache
        try
        {
            await _redisService.SetAsync(cacheKey, account, _cacheDuration);
            _logger.LogDebug("Cached account {Alias} for {Duration} minutes", accountAlias, _cacheDuration.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache account {Alias}", accountAlias);
        }

        return account;
    }
}
