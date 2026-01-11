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

public class FirebirdScheduledOrderRepository : IScheduledOrderRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FirebirdScheduledOrderRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FirebirdScheduledOrderRepository(IConfiguration configuration, ILogger<FirebirdScheduledOrderRepository> logger)
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
            "SELECT count(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'SCHEDULED_ORDERS'");

        if (tableExists == 0)
        {
            _logger.LogInformation("Creating SCHEDULED_ORDERS tables...");
            await conn.ExecuteAsync(@"
                CREATE TABLE SCHEDULED_ORDERS (
                    ID VARCHAR(38) PRIMARY KEY,
                    ACCOUNT_ALIAS VARCHAR(50) NOT NULL,
                    DATA BLOB SUB_TYPE TEXT NOT NULL,
                    LOCKED_AT TIMESTAMP,
                    LOCKED_BY VARCHAR(100),
                    UPDATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            await conn.ExecuteAsync("CREATE INDEX IX_SCHEDULED_ACCOUNT ON SCHEDULED_ORDERS (ACCOUNT_ALIAS)");
        }
    }

    public async Task<IEnumerable<ScheduledOrder>> GetAllAsync(string accountAlias)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT DATA FROM SCHEDULED_ORDERS WHERE ACCOUNT_ALIAS = @AccountAlias";
        var results = await conn.QueryAsync<string>(sql, new { AccountAlias = accountAlias });

        return results.Select(json => JsonSerializer.Deserialize<ScheduledOrder>(json, _jsonOptions)!)
                      .Where(o => o != null);
    }

    public async Task ProcessOrdersAsync(string accountAlias, Func<List<ScheduledOrder>, Task<bool>> processor, bool waitForLock = false)
    {
        using var conn = new FbConnection(_connectionString);
        await conn.OpenAsync();

        using var trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 1. Stale Lock Cleanup: If a lock was not cleared due to a crash (LOCKED_AT > 20h), reset it.
            // Firebird DATEADD(-20 HOUR TO CURRENT_TIMESTAMP) works in 2.5/3.0+
            await conn.ExecuteAsync(@"
                UPDATE SCHEDULED_ORDERS 
                SET LOCKED_AT = NULL, LOCKED_BY = NULL 
                WHERE ACCOUNT_ALIAS = @AccountAlias 
                  AND LOCKED_AT < DATEADD(-20 HOUR TO CURRENT_TIMESTAMP)",
                new { AccountAlias = accountAlias }, trans);

            // 2. Row-Level Locking: Try to lock all orders for this account.
            var lockSql = "SELECT ID FROM SCHEDULED_ORDERS WHERE ACCOUNT_ALIAS = @AccountAlias WITH LOCK";
            if (!waitForLock)
            {
                lockSql += " NO WAIT";
            }

            try
            {
                var lockIds = (await conn.QueryAsync<string>(lockSql, new { AccountAlias = accountAlias }, trans)).ToList();

                // 3. Mark Lock: If we acquired the database lock, update tracking fields for visibility
                if (lockIds.Any())
                {
                    await conn.ExecuteAsync(@"
                        UPDATE SCHEDULED_ORDERS 
                        SET LOCKED_AT = CURRENT_TIMESTAMP, LOCKED_BY = @Host 
                        WHERE ID IN @Ids",
                        new { Host = Environment.MachineName, Ids = lockIds }, trans);
                }
            }
            catch (FbException ex) when (ex.ErrorCode == 335544345) // Lock conflict on NO WAIT
            {
                _logger.LogWarning("Could not acquire lock for orders in account {Account}, skipping processing", accountAlias);
                trans.Rollback();
                return;
            }

            // Lock acquired (as long as transaction is open)
            var orders = (await conn.QueryAsync<string>(
                "SELECT DATA FROM SCHEDULED_ORDERS WHERE ACCOUNT_ALIAS = @AccountAlias",
                new { AccountAlias = accountAlias }, trans))
                .Select(json => JsonSerializer.Deserialize<ScheduledOrder>(json, _jsonOptions)!)
                .ToList();

            if (!orders.Any())
            {
                trans.Commit(); // Nothing to do
                return;
            }

            var shouldSave = await processor(orders);
            if (shouldSave)
            {
                // Delete missing orders
                var currentIds = orders.Select(o => o.Id.ToString()).ToList();
                await conn.ExecuteAsync(
                    "DELETE FROM SCHEDULED_ORDERS WHERE ACCOUNT_ALIAS = @AccountAlias AND ID NOT IN @Ids",
                    new { AccountAlias = accountAlias, Ids = currentIds.Any() ? currentIds : new List<string> { "None" } }, trans);

                // Update or Insert
                foreach (var order in orders)
                {
                    await conn.ExecuteAsync(@"
                        UPDATE OR INSERT INTO SCHEDULED_ORDERS (ID, ACCOUNT_ALIAS, DATA, LOCKED_AT, LOCKED_BY, UPDATED_AT)
                        VALUES (@Id, @AccountAlias, @Data, NULL, NULL, CURRENT_TIMESTAMP)
                        MATCHING (ID)",
                        new
                        {
                            Id = order.Id.ToString(),
                            AccountAlias = accountAlias,
                            Data = JsonSerializer.Serialize(order, _jsonOptions)
                        }, trans);
                }
            }
            else
            {
                // Reset lock info if not saving (processor just read them)
                await conn.ExecuteAsync(@"
                    UPDATE SCHEDULED_ORDERS 
                    SET LOCKED_AT = NULL, LOCKED_BY = NULL 
                    WHERE ACCOUNT_ALIAS = @AccountAlias",
                    new { AccountAlias = accountAlias }, trans);
            }

            trans.Commit();
        }
        catch (Exception ex)
        {
            trans.Rollback();
            _logger.LogError(ex, "Error processing orders for {Account} in Firebird", accountAlias);
            throw;
        }
    }

    public async Task AddOrUpdateAsync(ScheduledOrder order)
    {
        await ProcessOrdersAsync(order.AccountAlias, async (orders) =>
        {
            var existing = orders.FirstOrDefault(o => o.Id == order.Id);
            if (existing != null)
            {
                orders.Remove(existing);
            }
            orders.Add(order);
            return true;
        }, waitForLock: true);
    }

    public async Task RemoveAsync(string accountAlias, Guid orderId)
    {
        await ProcessOrdersAsync(accountAlias, async (orders) =>
        {
            var existing = orders.FirstOrDefault(o => o.Id == orderId);
            if (existing != null)
            {
                orders.Remove(existing);
                return true;
            }
            return false;
        }, waitForLock: true);
    }
}
