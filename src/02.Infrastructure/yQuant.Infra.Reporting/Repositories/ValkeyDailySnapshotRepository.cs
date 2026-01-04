using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Repositories;

/// <summary>
/// Valkey 기반 일별 스냅샷 저장소
/// 키: snapshots:{account}:{YYYY-MM-DD} (Hash)
/// Field: {currency}, Value: DailySnapshot JSON (positions 포함)
/// </summary>
public class ValkeyDailySnapshotRepository : IDailySnapshotRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeyDailySnapshotRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public ValkeyDailySnapshotRepository(
        IConnectionMultiplexer redis,
        ILogger<ValkeyDailySnapshotRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SaveAsync(string accountAlias, DailySnapshot snapshot)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetDailyKey(accountAlias, snapshot.Date);
            var field = snapshot.Currency.ToString();
            var value = JsonSerializer.Serialize(snapshot, _jsonOptions);

            await db.HashSetAsync(key, field, value);

            _logger.LogInformation(
                "Saved snapshot for {Account} on {Date} ({Currency}): Equity={Equity:N2}, Return={Return:P2}",
                accountAlias, snapshot.Date, snapshot.Currency, snapshot.TotalEquity, snapshot.DailyReturn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snapshot for {Account} on {Date} ({Currency})",
                accountAlias, snapshot.Date, snapshot.Currency);
            throw;
        }
    }

    public async Task<DailySnapshot?> GetSnapshotByDateAsync(string accountAlias, DateOnly date)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetDailyKey(accountAlias, date);

            // Get all currencies for this date
            var entries = await db.HashGetAllAsync(key);

            if (entries.Length == 0)
                return null;

            // Return first currency (or you can specify which one)
            var firstEntry = entries.First();
            return JsonSerializer.Deserialize<DailySnapshot>(firstEntry.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get snapshot for {Account} on {Date}", accountAlias, date);
            return null;
        }
    }

    public async Task<IEnumerable<DailySnapshot>> GetSnapshotsByRangeAsync(
        string accountAlias,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var snapshots = new List<DailySnapshot>();
            var db = _redis.GetDatabase();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var key = GetDailyKey(accountAlias, date);
                var entries = await db.HashGetAllAsync(key);

                foreach (var entry in entries)
                {
                    var snapshot = JsonSerializer.Deserialize<DailySnapshot>(entry.Value.ToString());
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
            }

            return snapshots.OrderBy(s => s.Date).ThenBy(s => s.Currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get snapshots for {Account} from {Start} to {End}",
                accountAlias, startDate, endDate);
            return Enumerable.Empty<DailySnapshot>();
        }
    }

    public async Task<IEnumerable<DailySnapshot>> GetAllSnapshotsAsync(string accountAlias)
    {
        try
        {
            var snapshots = new List<DailySnapshot>();
            var db = _redis.GetDatabase();

            // Get all snapshot keys for this account
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"snapshots:{accountAlias}:*";
            var keys = server.Keys(pattern: pattern).ToList();

            foreach (var key in keys)
            {
                var entries = await db.HashGetAllAsync(key);

                foreach (var entry in entries)
                {
                    var snapshot = JsonSerializer.Deserialize<DailySnapshot>(entry.Value.ToString());
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
            }

            return snapshots.OrderBy(s => s.Date).ThenBy(s => s.Currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all snapshots for {Account}", accountAlias);
            return Enumerable.Empty<DailySnapshot>();
        }
    }

    public async Task<DailySnapshot?> GetLatestSnapshotAsync(string accountAlias)
    {
        try
        {
            var snapshots = await GetAllSnapshotsAsync(accountAlias);
            return snapshots.OrderByDescending(s => s.Date).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest snapshot for {Account}", accountAlias);
            return null;
        }
    }

    private string GetDailyKey(string accountAlias, DateOnly date)
    {
        return $"snapshots:{accountAlias}:{date:yyyy-MM-dd}";
    }
}
