using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Repositories;

/// <summary>
/// Valkey 기반 거래 실적 저장소
/// 키: trades:{account}:{YYYY-MM} (Sorted Set)
/// Score: Unix timestamp, Value: TradeRecord JSON
/// </summary>
public class ValkeyTradeRepository : ITradeRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeyTradeRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public ValkeyTradeRepository(
        IConnectionMultiplexer redis,
        ILogger<ValkeyTradeRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SaveAsync(string accountAlias, TradeRecord trade)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetMonthlyKey(accountAlias, trade.ExecutedAt);
            var score = new DateTimeOffset(trade.ExecutedAt).ToUnixTimeSeconds();
            var value = JsonSerializer.Serialize(trade, _jsonOptions);

            await db.SortedSetAddAsync(key, value, score);

            _logger.LogInformation(
                "Saved trade {TradeId} for {Account} to Valkey key {Key}",
                trade.Id, accountAlias, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save trade {TradeId} for {Account}", trade.Id, accountAlias);
            throw;
        }
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByDateAsync(string accountAlias, DateOnly date)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetMonthlyKey(accountAlias, date.ToDateTime(TimeOnly.MinValue));

            var startOfDay = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
            var endOfDay = new DateTimeOffset(date.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero).ToUnixTimeSeconds();

            var values = await db.SortedSetRangeByScoreAsync(key, startOfDay, endOfDay);

            return values
                .Select(v => JsonSerializer.Deserialize<TradeRecord>(v.ToString()))
                .Where(t => t != null)
                .Cast<TradeRecord>()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trades for {Account} on {Date}", accountAlias, date);
            return Enumerable.Empty<TradeRecord>();
        }
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByRangeAsync(
        string accountAlias,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var trades = new List<TradeRecord>();
            var db = _redis.GetDatabase();

            // Get all months in range
            var months = GetMonthsInRange(startDate, endDate);

            foreach (var month in months)
            {
                var key = GetMonthlyKey(accountAlias, month);

                var startTimestamp = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
                var endTimestamp = new DateTimeOffset(endDate.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero).ToUnixTimeSeconds();

                var values = await db.SortedSetRangeByScoreAsync(key, startTimestamp, endTimestamp);

                var monthTrades = values
                    .Select(v => JsonSerializer.Deserialize<TradeRecord>(v.ToString()))
                    .Where(t => t != null)
                    .Cast<TradeRecord>();

                trades.AddRange(monthTrades);
            }

            return trades.OrderBy(t => t.ExecutedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trades for {Account} from {Start} to {End}",
                accountAlias, startDate, endDate);
            return Enumerable.Empty<TradeRecord>();
        }
    }

    public async Task<IEnumerable<TradeRecord>> GetAllTradesAsync(string accountAlias)
    {
        try
        {
            var trades = new List<TradeRecord>();
            var db = _redis.GetDatabase();

            // Get all keys for this account
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"trades:{accountAlias}:*";
            var keys = server.Keys(pattern: pattern).ToList();

            foreach (var key in keys)
            {
                var values = await db.SortedSetRangeByScoreAsync(key);

                var keyTrades = values
                    .Select(v => JsonSerializer.Deserialize<TradeRecord>(v.ToString()))
                    .Where(t => t != null)
                    .Cast<TradeRecord>();

                trades.AddRange(keyTrades);
            }

            return trades.OrderBy(t => t.ExecutedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all trades for {Account}", accountAlias);
            return Enumerable.Empty<TradeRecord>();
        }
    }

    private string GetMonthlyKey(string accountAlias, DateTime date)
    {
        return $"trades:{accountAlias}:{date:yyyy-MM}";
    }

    private IEnumerable<DateTime> GetMonthsInRange(DateOnly startDate, DateOnly endDate)
    {
        var months = new List<DateTime>();
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        var end = new DateTime(endDate.Year, endDate.Month, 1);

        while (current <= end)
        {
            months.Add(current);
            current = current.AddMonths(1);
        }

        return months;
    }
}
