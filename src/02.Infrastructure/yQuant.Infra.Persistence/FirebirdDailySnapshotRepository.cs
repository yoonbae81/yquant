using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

/// <summary>
/// Firebird-based Daily Snapshot Repository.
/// Migrated from Valkey to support persistent, shared storage.
/// </summary>
public class FirebirdDailySnapshotRepository : IDailySnapshotRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FirebirdDailySnapshotRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FirebirdDailySnapshotRepository(IConfiguration configuration, ILogger<FirebirdDailySnapshotRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("Firebird")
            ?? throw new InvalidOperationException("Firebird connection string is missing.");
        _logger = logger;
    }

    private IDbConnection CreateConnection() => new FbConnection(_connectionString);

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        conn.Open();

        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'DAILY_SNAPSHOTS'");

        if (tableExists == 0)
        {
            _logger.LogInformation("Creating DAILY_SNAPSHOTS table...");
            await conn.ExecuteAsync(@"
                CREATE TABLE DAILY_SNAPSHOTS (
                    ACCOUNT_ALIAS VARCHAR(50) NOT NULL,
                    SNAPSHOT_DATE DATE NOT NULL,
                    CURRENCY VARCHAR(10) NOT NULL,
                    DATA BLOB SUB_TYPE TEXT NOT NULL,
                    UPDATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (ACCOUNT_ALIAS, SNAPSHOT_DATE, CURRENCY)
                )");

            await conn.ExecuteAsync("CREATE INDEX IX_SNAPSHOT_ACCOUNT_DATE ON DAILY_SNAPSHOTS (ACCOUNT_ALIAS, SNAPSHOT_DATE)");
        }
    }

    public async Task SaveAsync(string accountAlias, DailySnapshot snapshot)
    {
        using var conn = CreateConnection();
        const string sql = @"
            UPDATE OR INSERT INTO DAILY_SNAPSHOTS (ACCOUNT_ALIAS, SNAPSHOT_DATE, CURRENCY, DATA, UPDATED_AT)
            VALUES (@AccountAlias, @Date, @Currency, @Data, CURRENT_TIMESTAMP)
            MATCHING (ACCOUNT_ALIAS, SNAPSHOT_DATE, CURRENCY)";

        await conn.ExecuteAsync(sql, new
        {
            AccountAlias = accountAlias,
            Date = snapshot.Date.ToDateTime(TimeOnly.MinValue),
            Currency = snapshot.Currency.ToString(),
            Data = JsonSerializer.Serialize(snapshot, _jsonOptions)
        });

        _logger.LogInformation("Saved snapshot for {Account} on {Date} to Firebird.", accountAlias, snapshot.Date);
    }

    public async Task<DailySnapshot?> GetSnapshotByDateAsync(string accountAlias, DateOnly date)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT DATA FROM DAILY_SNAPSHOTS WHERE ACCOUNT_ALIAS = @AccountAlias AND SNAPSHOT_DATE = @Date";

        // Return first one found (usually only one per date/currency, but if multiple currencies exist, we pick one)
        var json = await conn.QueryFirstOrDefaultAsync<string>(sql, new
        {
            AccountAlias = accountAlias,
            Date = date.ToDateTime(TimeOnly.MinValue)
        });

        return json != null ? JsonSerializer.Deserialize<DailySnapshot>(json, _jsonOptions) : null;
    }

    public async Task<IEnumerable<DailySnapshot>> GetSnapshotsByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate)
    {
        using var conn = CreateConnection();
        const string sql = @"
            SELECT DATA FROM DAILY_SNAPSHOTS 
            WHERE ACCOUNT_ALIAS = @AccountAlias 
              AND SNAPSHOT_DATE BETWEEN @Start AND @End 
            ORDER BY SNAPSHOT_DATE ASC, CURRENCY ASC";

        var results = await conn.QueryAsync<string>(sql, new
        {
            AccountAlias = accountAlias,
            Start = startDate.ToDateTime(TimeOnly.MinValue),
            End = endDate.ToDateTime(TimeOnly.MinValue)
        });

        return results.Select(json => JsonSerializer.Deserialize<DailySnapshot>(json, _jsonOptions)!);
    }

    public async Task<IEnumerable<DailySnapshot>> GetAllSnapshotsAsync(string accountAlias)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT DATA FROM DAILY_SNAPSHOTS WHERE ACCOUNT_ALIAS = @AccountAlias ORDER BY SNAPSHOT_DATE ASC, CURRENCY ASC";
        var results = await conn.QueryAsync<string>(sql, new { AccountAlias = accountAlias });

        return results.Select(json => JsonSerializer.Deserialize<DailySnapshot>(json, _jsonOptions)!);
    }

    public async Task<DailySnapshot?> GetLatestSnapshotAsync(string accountAlias)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT FIRST 1 DATA FROM DAILY_SNAPSHOTS WHERE ACCOUNT_ALIAS = @AccountAlias ORDER BY SNAPSHOT_DATE DESC";
        var json = await conn.QueryFirstOrDefaultAsync<string>(sql, new { AccountAlias = accountAlias });

        return json != null ? JsonSerializer.Deserialize<DailySnapshot>(json, _jsonOptions) : null;
    }
}
