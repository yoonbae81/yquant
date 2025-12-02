using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Adapters;

namespace yQuant.App.Dashboard.Services;

public class RedisBrokerAdapterFactory : IBrokerAdapterFactory
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisBrokerAdapterFactory> _logger;
    private readonly IConfiguration _configuration;
    private List<string> _cachedAccounts = new();
    private readonly List<string> _configuredAccounts = new();
    private readonly object _lock = new();


    public RedisBrokerAdapterFactory(IConnectionMultiplexer redis, ILogger<RedisBrokerAdapterFactory> logger, IConfiguration configuration)
    {
        _redis = redis;
        _logger = logger;
        _configuration = configuration;

        // Load accounts from configuration
        var accountsSection = _configuration.GetSection("Accounts");
        var accounts = accountsSection.Get<List<AccountConfig>>();
        if (accounts != null)
        {
            _configuredAccounts = accounts.Select(a => a.Alias).ToList();
            lock (_lock)
            {
                _cachedAccounts = _configuredAccounts.ToList();
            }
            _logger.LogInformation("Loaded {Count} accounts from configuration: {Accounts}", _configuredAccounts.Count, string.Join(", ", _configuredAccounts));
        }

        // Fetch accounts from Redis once at startup
        try
        {
            var db = _redis.GetDatabase();
            var json = db.StringGet("broker:available_accounts");

            if (json.HasValue)
            {
                var redisAccounts = JsonSerializer.Deserialize<List<string>>(json.ToString());
                if (redisAccounts != null)
                {
                    lock (_lock)
                    {
                        // Merge configured accounts with Redis accounts, ensuring no duplicates
                        _cachedAccounts = _configuredAccounts.Union(redisAccounts).ToList();
                    }
                    _logger.LogInformation("Loaded accounts from Redis. Configured: {ConfigCount}, Redis: {RedisCount}, Total: {TotalCount}",
                        _configuredAccounts.Count, redisAccounts.Count, _cachedAccounts.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading accounts from Redis Registry at startup");
        }
    }

    public IBrokerAdapter? GetAdapter(string alias)
    {
        return new RedisBrokerClient(_redis, alias);
    }



    public IEnumerable<string> GetAvailableAccounts()
    {
        lock (_lock)
        {
            return _cachedAccounts.ToList();
        }
    }





    // Helper class for configuration binding
    private class AccountConfig
    {
        public string Alias { get; set; } = "";
    }
}
