using Microsoft.Extensions.Logging;
using yQuant.Core.Models;

namespace yQuant.App.Web.Services;

/// <summary>
/// Client-side cache service for account information to reduce redundant Redis calls
/// </summary>
public class AccountCacheService
{
    private readonly AssetService _assetService;
    private readonly ILogger<AccountCacheService> _logger;

    private List<Account>? _cachedAccounts;
    private DateTime? _cacheTimestamp;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5); // Cache for 5 minutes
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public AccountCacheService(
        AssetService assetService,
        ILogger<AccountCacheService> logger)
    {
        _assetService = assetService;
        _logger = logger;
    }

    /// <summary>
    /// Get cached account list or fetch from Redis if cache is expired
    /// </summary>
    public virtual async Task<List<Account>> GetAccountsAsync(bool forceRefresh = false)
    {
        await _cacheLock.WaitAsync();
        try
        {
            // Check if cache is valid
            if (!forceRefresh &&
                _cachedAccounts != null &&
                _cacheTimestamp.HasValue &&
                DateTime.UtcNow - _cacheTimestamp.Value < _cacheExpiration)
            {
                _logger.LogDebug("Returning {Count} accounts from client cache", _cachedAccounts.Count);
                return _cachedAccounts;
            }

            // Cache expired or force refresh - fetch from Redis
            _logger.LogInformation("Fetching accounts from Redis (cache expired or force refresh)");
            var aliases = await _assetService.GetAvailableAccountsAsync();
            var accounts = new List<Account>();

            foreach (var alias in aliases)
            {
                var account = await _assetService.GetAccountBasicInfo(alias);
                if (account != null)
                {
                    accounts.Add(account);
                }
            }

            _cachedAccounts = accounts;
            _cacheTimestamp = DateTime.UtcNow;

            _logger.LogInformation("Cached {Count} accounts: {Accounts}",
                accounts.Count,
                string.Join(", ", accounts.Select(a => a.Alias)));

            return _cachedAccounts;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Clear the cache to force a refresh on next access
    /// </summary>
    public virtual void ClearCache()
    {
        _logger.LogInformation("Clearing account cache");
        _cachedAccounts = null;
        _cacheTimestamp = null;
    }

    /// <summary>
    /// Get a specific account from cache
    /// </summary>
    public virtual async Task<Account?> GetAccountAsync(string alias)
    {
        var accounts = await GetAccountsAsync();
        return accounts.FirstOrDefault(a => a.Alias == alias);
    }
}
