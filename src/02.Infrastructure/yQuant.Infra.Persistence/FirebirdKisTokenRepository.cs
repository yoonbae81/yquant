using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class FirebirdKisTokenRepository : IKisTokenRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FirebirdKisTokenRepository> _logger;

    public FirebirdKisTokenRepository(IConfiguration configuration, ILogger<FirebirdKisTokenRepository> logger)
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
            "SELECT count(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'TOKENS'");

        if (tableExists == 0)
        {
            _logger.LogInformation("Creating TOKENS table...");
            await conn.ExecuteAsync(@"
                CREATE TABLE TOKENS (
                    ACCOUNT_ALIAS VARCHAR(50) PRIMARY KEY,
                    BROKER VARCHAR(20) NOT NULL,
                    TOKEN VARCHAR(2000) NOT NULL,
                    EXPIRATION TIMESTAMP NOT NULL,
                    UPDATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }
    }

    public async Task<TokenCacheEntry?> GetTokenAsync(string accountAlias)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT TOKEN, EXPIRATION FROM TOKENS WHERE ACCOUNT_ALIAS = @AccountAlias AND BROKER = 'KIS'";
        return await conn.QueryFirstOrDefaultAsync<TokenCacheEntry>(sql, new { AccountAlias = accountAlias });
    }

    public async Task SaveTokenAsync(string accountAlias, TokenCacheEntry entry)
    {
        using var conn = CreateConnection();
        const string sql = @"
            UPDATE OR INSERT INTO TOKENS (ACCOUNT_ALIAS, BROKER, TOKEN, EXPIRATION, UPDATED_AT)
            VALUES (@AccountAlias, 'KIS', @Token, @Expiration, CURRENT_TIMESTAMP)
            MATCHING (ACCOUNT_ALIAS)";

        await conn.ExecuteAsync(sql, new
        {
            AccountAlias = accountAlias,
            entry.Token,
            entry.Expiration
        });
    }

    public async Task DeleteTokenAsync(string accountAlias)
    {
        using var conn = CreateConnection();
        const string sql = "DELETE FROM TOKENS WHERE ACCOUNT_ALIAS = @AccountAlias AND BROKER = 'KIS'";
        await conn.ExecuteAsync(sql, new { AccountAlias = accountAlias });
    }
}
