using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Extensions;

namespace yQuant.App.OrderManager.Adapters;

public class ValkeyAccountRepository : IAccountRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeyAccountRepository> _logger;

    public ValkeyAccountRepository(IConnectionMultiplexer redis, ILogger<ValkeyAccountRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<Account?> GetAccountAsync(string accountAlias)
    {
        var db = _redis.GetDatabase();

        // 1. Fetch Static Info
        var accountKey = $"account:{accountAlias}";
        var accountEntries = await db.HashGetAllAsync(accountKey);

        if (accountEntries.Length == 0)
        {
            _logger.LogWarning("Account {AccountAlias} not found in Valkey.", accountAlias);
            return null;
        }

        var accountDict = accountEntries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        // 2. Fetch Deposits
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

        // 3. Fetch Positions
        var positionKey = $"position:{accountAlias}";
        var positionEntries = await db.HashGetAllAsync(positionKey);
        var positions = new List<Position>();

        foreach (var entry in positionEntries)
        {
            try
            {
                var position = JsonSerializer.Deserialize<Position>(entry.Value.ToString());
                if (position != null)
                {
                    positions.Add(position);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize position for {Ticker} in account {AccountAlias}", entry.Name, accountAlias);
            }
        }

        // 4. Aggregate
        // Group by Country Code derived from Currency
        var groupedPositions = positions
            .GroupBy(p => p.Currency.GetCountryCode())
            .ToDictionary(g => g.Key, g => g.ToList());

        return new Account
        {
            Alias = accountAlias,
            Number = accountDict.GetValueOrDefault("number", string.Empty),
            Broker = accountDict.GetValueOrDefault("broker", string.Empty),
            Active = bool.TryParse(accountDict.GetValueOrDefault("is_active"), out var isActive) && isActive,
            AppKey = string.Empty, // Redacted
            AppSecret = string.Empty, // Redacted
            Deposits = deposits,
            Positions = groupedPositions
        };
    }
}
