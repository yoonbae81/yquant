using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Valkey.Interfaces;
using StackExchange.Redis;
using System.Text.Json;
using yQuant.Core.Extensions;

namespace yQuant.App.Dashboard.Services;

public class AssetService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IValkeyService _redisService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssetService> _logger;
    private readonly TimeSpan _cacheDuration;

    public AssetService(
        IConnectionMultiplexer redis,
        IValkeyService redisService,
        IConfiguration configuration,
        ILogger<AssetService> logger)
    {
        _redis = redis;
        _redisService = redisService;
        _configuration = configuration;
        _logger = logger;

        var minutes = _configuration.GetValue<int>("Web:Cache:AssetCacheDurationMinutes", 1);
        _cacheDuration = TimeSpan.FromMinutes(minutes);
    }

    public virtual async Task<List<string>> GetAvailableAccountsAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            // Read from account:index Set
            var members = await db.SetMembersAsync("account:index");
            if (members.Length > 0)
            {
                var accounts = members.Select(m => m.ToString()).ToList();
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Fetched {Count} accounts from Valkey Index: {Accounts}", accounts.Count, string.Join(", ", accounts));
                }
                return accounts;
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to fetch available accounts from Valkey");
            }
        }
        return [];
    }

    public async Task<Account?> GetAccountBasicInfo(string alias)
    {
        try
        {
            var db = _redis.GetDatabase();
            var accountKey = $"account:{alias}";
            var accountEntries = await db.HashGetAllAsync(accountKey);

            if (accountEntries.Length == 0) return null;

            var accountDict = accountEntries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

            return new Account
            {
                Alias = alias,
                Broker = accountDict.GetValueOrDefault("broker", "Unknown"),
                Number = accountDict.GetValueOrDefault("number", "N/A"),
                AppKey = "N/A",
                AppSecret = "N/A",
                Deposits = [],
                Active = bool.TryParse(accountDict.GetValueOrDefault("is_active"), out var isActive) && isActive
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch basic account info for {Alias}", alias);
            return null;
        }
    }

    public virtual async Task<Account?> GetAccountOverviewAsync(string accountAlias)
    {
        // Direct Valkey Read (Aggregation)
        // No local caching needed here if we consider Valkey as the "State Store".
        // But if we want to reduce Valkey calls, we can cache the aggregated object.
        // Given the requirement for "Real-time", let's read directly from Valkey State keys.
        // They are updated by BrokerGateway periodically (e.g. 10s).

        try
        {
            var db = _redis.GetDatabase();

            // 1. Static Info
            var accountKey = $"account:{accountAlias}";
            var accountEntries = await db.HashGetAllAsync(accountKey);
            if (accountEntries.Length == 0) return null;

            var accountDict = accountEntries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

            // 2. Deposits
            var depositKey = $"deposit:{accountAlias}";
            var depositEntries = await db.HashGetAllAsync(depositKey);
            var deposits = new Dictionary<CurrencyType, decimal>();
            foreach (var entry in depositEntries)
            {
                if (Enum.TryParse<CurrencyType>(entry.Name, out var currency) && decimal.TryParse(entry.Value.ToString(), out var amount))
                {
                    deposits[currency] = amount;
                }
            }

            // 3. Positions
            var positionKey = $"position:{accountAlias}";
            var positionEntries = await db.HashGetAllAsync(positionKey);
            var positions = new List<Position>();
            foreach (var entry in positionEntries)
            {
                try
                {
                    var position = JsonSerializer.Deserialize<Position>(entry.Value.ToString());
                    if (position != null) positions.Add(position);
                }
                catch { /* Ignore invalid position json */ }
            }

            // Group by Country Code derived from Currency
            var groupedPositions = positions
                .GroupBy(p => p.Currency.GetCountryCode())
                .ToDictionary(g => g.Key, g => g.ToList());

            return new Account
            {
                Alias = accountAlias,
                Number = accountDict.GetValueOrDefault("number", "N/A"),
                Broker = accountDict.GetValueOrDefault("broker", "Unknown"),
                Active = bool.TryParse(accountDict.GetValueOrDefault("is_active"), out var isActive) && isActive,
                AppKey = "",
                AppSecret = "",
                Deposits = deposits,
                Positions = groupedPositions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to aggregate account data for {Alias}", accountAlias);
            return null;
        }
    }
}
