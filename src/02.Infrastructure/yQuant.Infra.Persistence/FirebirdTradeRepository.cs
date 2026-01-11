using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class FirebirdTradeRepository : ITradeRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FirebirdTradeRepository> _logger;

    public FirebirdTradeRepository(IConfiguration configuration, ILogger<FirebirdTradeRepository> logger)
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

        _logger.LogInformation("Initializing Firebird Schema...");

        // Check if table exists
        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'TRADES'");

        if (tableExists == 0)
        {
            _logger.LogInformation("Creating TRADES table...");
            await conn.ExecuteAsync(@"
                CREATE TABLE TRADES (
                    ID CHAR(38) PRIMARY KEY,
                    EXECUTED_AT TIMESTAMP NOT NULL,
                    TICKER VARCHAR(20) NOT NULL,
                    ACTION VARCHAR(10) NOT NULL,
                    QUANTITY DECIMAL(18,4) NOT NULL,
                    PRICE DECIMAL(18,4) NOT NULL,
                    AMOUNT DECIMAL(18,4) NOT NULL,
                    COMMISSION DECIMAL(18,4) NOT NULL,
                    ORDER_ID VARCHAR(50),
                    BROKER_ORDER_ID VARCHAR(50),
                    STRATEGY VARCHAR(50),
                    CURRENCY VARCHAR(10) NOT NULL,
                    EXCHANGE VARCHAR(20) NOT NULL,
                    ACCOUNT_ALIAS VARCHAR(50) NOT NULL
                )");

            await conn.ExecuteAsync("CREATE INDEX IX_TRADES_ACCOUNT_DATE ON TRADES (ACCOUNT_ALIAS, EXECUTED_AT)");
        }
    }

    public async Task SaveAsync(string accountAlias, TradeRecord trade)
    {
        using var conn = CreateConnection();
        var entity = TradeRecordEntity.FromModel(accountAlias, trade);

        const string sql = @"
            UPDATE OR INSERT INTO TRADES (
                ID, EXECUTED_AT, TICKER, ACTION, QUANTITY, PRICE, AMOUNT, 
                COMMISSION, ORDER_ID, BROKER_ORDER_ID, STRATEGY, 
                CURRENCY, EXCHANGE, ACCOUNT_ALIAS
            ) VALUES (
                @Id, @ExecutedAt, @Ticker, @Action, @Quantity, @ExecutedPrice, @Amount, 
                @Commission, @OrderId, @BrokerOrderId, @Strategy, 
                @Currency, @Exchange, @AccountAlias
            ) MATCHING (ID)";

        await conn.ExecuteAsync(sql, entity);
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByDateAsync(string accountAlias, DateOnly date)
    {
        using var conn = CreateConnection();
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = date.ToDateTime(TimeOnly.MaxValue);

        const string sql = "SELECT * FROM TRADES WHERE ACCOUNT_ALIAS = @AccountAlias AND EXECUTED_AT BETWEEN @start AND @end ORDER BY EXECUTED_AT DESC";
        var entities = await conn.QueryAsync<TradeRecordEntity>(sql, new { AccountAlias = accountAlias, start, end });
        return entities.Select(e => e.ToModel());
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate)
    {
        using var conn = CreateConnection();
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var end = endDate.ToDateTime(TimeOnly.MaxValue);

        const string sql = "SELECT * FROM TRADES WHERE ACCOUNT_ALIAS = @AccountAlias AND EXECUTED_AT BETWEEN @start AND @end ORDER BY EXECUTED_AT DESC";
        var entities = await conn.QueryAsync<TradeRecordEntity>(sql, new { AccountAlias = accountAlias, start, end });
        return entities.Select(e => e.ToModel());
    }

    public async Task<IEnumerable<TradeRecord>> GetAllTradesAsync(string accountAlias)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT * FROM TRADES WHERE ACCOUNT_ALIAS = @AccountAlias ORDER BY EXECUTED_AT DESC";
        var entities = await conn.QueryAsync<TradeRecordEntity>(sql, new { AccountAlias = accountAlias });
        return entities.Select(e => e.ToModel());
    }

    private class TradeRecordEntity
    {
        public string Id { get; set; } = null!;
        public DateTime ExecutedAt { get; set; }
        public string Ticker { get; set; } = null!;
        public string Action { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal ExecutedPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal Commission { get; set; }
        public string? OrderId { get; set; }
        public string? BrokerOrderId { get; set; }
        public string? Strategy { get; set; }
        public string Currency { get; set; } = null!;
        public string Exchange { get; set; } = null!;
        public string AccountAlias { get; set; } = null!;

        public static TradeRecordEntity FromModel(string accountAlias, TradeRecord model) => new()
        {
            Id = model.Id.ToString().ToUpper(),
            ExecutedAt = model.ExecutedAt,
            Ticker = model.Ticker,
            Action = model.Action.ToString(),
            Quantity = model.Quantity,
            ExecutedPrice = model.ExecutedPrice,
            Amount = model.Amount,
            Commission = model.Commission,
            OrderId = model.OrderId,
            BrokerOrderId = model.BrokerOrderId,
            Strategy = model.Strategy,
            Currency = model.Currency.ToString(),
            Exchange = model.Exchange.ToString(),
            AccountAlias = accountAlias
        };

        public TradeRecord ToModel() => new()
        {
            Id = Guid.Parse(Id),
            ExecutedAt = ExecutedAt,
            Ticker = Ticker,
            Action = Enum.Parse<OrderAction>(Action),
            Quantity = Quantity,
            ExecutedPrice = ExecutedPrice,
            Commission = Commission,
            OrderId = OrderId,
            BrokerOrderId = BrokerOrderId,
            Strategy = Strategy,
            Currency = Enum.Parse<CurrencyType>(Currency),
            Exchange = Enum.Parse<ExchangeCode>(Exchange)
        };
    }
}
