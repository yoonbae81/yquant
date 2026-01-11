using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

/// <summary>
/// MariaDB-based Daily Snapshot Repository.
/// Migrated from Firebird to support persistent, shared storage.
/// </summary>
public class MariaDbDailySnapshotRepository : IDailySnapshotRepository
{
    private readonly MariaDbContext _context;
    private readonly ILogger<MariaDbDailySnapshotRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public MariaDbDailySnapshotRepository(MariaDbContext context, ILogger<MariaDbDailySnapshotRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveAsync(string accountAlias, DailySnapshot snapshot)
    {
        var snapshotDate = snapshot.Date.ToDateTime(TimeOnly.MinValue);
        var currency = snapshot.Currency.ToString();

        var entity = await _context.DailySnapshots
            .FindAsync(accountAlias, snapshotDate, currency);

        if (entity != null)
        {
            entity.Data = JsonSerializer.Serialize(snapshot, _jsonOptions);
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await _context.DailySnapshots.AddAsync(new DailySnapshotEntity
            {
                AccountAlias = accountAlias,
                SnapshotDate = snapshotDate,
                Currency = currency,
                Data = JsonSerializer.Serialize(snapshot, _jsonOptions),
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Saved snapshot for {Account} on {Date} to MariaDB.", accountAlias, snapshot.Date);
    }

    public async Task<DailySnapshot?> GetSnapshotByDateAsync(string accountAlias, DateOnly date)
    {
        var snapshotDate = date.ToDateTime(TimeOnly.MinValue);

        var entity = await _context.DailySnapshots
            .Where(s => s.AccountAlias == accountAlias && s.SnapshotDate == snapshotDate)
            .FirstOrDefaultAsync();

        return entity != null
            ? JsonSerializer.Deserialize<DailySnapshot>(entity.Data, _jsonOptions)
            : null;
    }

    public async Task<IEnumerable<DailySnapshot>> GetSnapshotsByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var end = endDate.ToDateTime(TimeOnly.MinValue);

        var entities = await _context.DailySnapshots
            .Where(s => s.AccountAlias == accountAlias && s.SnapshotDate >= start && s.SnapshotDate <= end)
            .OrderBy(s => s.SnapshotDate)
            .ThenBy(s => s.Currency)
            .ToListAsync();

        return entities.Select(e => JsonSerializer.Deserialize<DailySnapshot>(e.Data, _jsonOptions)!);
    }

    public async Task<IEnumerable<DailySnapshot>> GetAllSnapshotsAsync(string accountAlias)
    {
        var entities = await _context.DailySnapshots
            .Where(s => s.AccountAlias == accountAlias)
            .OrderBy(s => s.SnapshotDate)
            .ThenBy(s => s.Currency)
            .ToListAsync();

        return entities.Select(e => JsonSerializer.Deserialize<DailySnapshot>(e.Data, _jsonOptions)!);
    }

    public async Task<DailySnapshot?> GetLatestSnapshotAsync(string accountAlias)
    {
        var entity = await _context.DailySnapshots
            .Where(s => s.AccountAlias == accountAlias)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync();

        return entity != null
            ? JsonSerializer.Deserialize<DailySnapshot>(entity.Data, _jsonOptions)
            : null;
    }
}
