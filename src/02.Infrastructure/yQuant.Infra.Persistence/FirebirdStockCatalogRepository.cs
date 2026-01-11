using System.Collections.Concurrent;
using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class FirebirdStockCatalogRepository : IStockCatalogRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FirebirdStockCatalogRepository> _logger;
    private readonly ISystemLogger _systemLogger;

    private static readonly ConcurrentDictionary<string, Stock> _memoryCache = new();
    private static bool _isCacheLoaded = false;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public FirebirdStockCatalogRepository(IConfiguration configuration, ILogger<FirebirdStockCatalogRepository> logger, ISystemLogger systemLogger)
    {
        _connectionString = configuration.GetConnectionString("Firebird")
            ?? throw new InvalidOperationException("Firebird connection string is missing.");
        _logger = logger;
        _systemLogger = systemLogger;
    }

    private IDbConnection CreateConnection() => new FbConnection(_connectionString);

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        conn.Open();

        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'CATALOG'");

        if (tableExists == 0)
        {
            _logger.LogInformation("Creating CATALOG tables...");
            await conn.ExecuteAsync(@"
                CREATE TABLE CATALOG (
                    TICKER VARCHAR(20) PRIMARY KEY,
                    NAME VARCHAR(200) NOT NULL,
                    EXCHANGE VARCHAR(20) NOT NULL,
                    CURRENCY VARCHAR(10) NOT NULL,
                    COUNTRY VARCHAR(10) NOT NULL,
                    LAST_UPDATED TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            await conn.ExecuteAsync(@"
                CREATE TABLE CATALOG_METADATA (
                    KEY_NAME VARCHAR(50) PRIMARY KEY,
                    VALUE_TEXT VARCHAR(200) NOT NULL,
                    UPDATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }
    }

    public async Task SaveBatchAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            const string sql = @"
                UPDATE OR INSERT INTO CATALOG (TICKER, NAME, EXCHANGE, CURRENCY, COUNTRY, LAST_UPDATED)
                VALUES (@Ticker, @Name, @Exchange, @Currency, @Country, CURRENT_TIMESTAMP)
                MATCHING (TICKER)";

            foreach (var stock in stocks)
            {
                var country = GetCountryByExchange(stock.Exchange);
                await conn.ExecuteAsync(sql, new
                {
                    stock.Ticker,
                    stock.Name,
                    stock.Exchange,
                    Currency = stock.Currency.ToString(),
                    Country = country
                }, trans);

                _memoryCache[stock.Ticker] = stock;
            }

            trans.Commit();
            _logger.LogInformation("Saved {Count} stocks to Firebird Stock Catalog.", stocks.Count());
        }
        catch (Exception ex)
        {
            trans.Rollback();
            _logger.LogError(ex, "Failed to save stock batch to Firebird.");
            throw;
        }
    }

    public async Task<Stock?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(ticker, out var cached)) return cached;

        using var conn = CreateConnection();
        const string sql = "SELECT * FROM CATALOG WHERE TICKER = @Ticker";
        var entity = await conn.QueryFirstOrDefaultAsync<StockEntity>(sql, new { Ticker = ticker });

        if (entity == null) return null;

        var stock = entity.ToModel();
        _memoryCache[ticker] = stock;
        return stock;
    }

    public async Task LoadAllToMemoryAsync(bool force = false, string? countryCode = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(countryCode))
        {
            await LoadCountryToMemoryAsync(countryCode, cancellationToken);
            return;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_isCacheLoaded && !force) return;

            using var conn = CreateConnection();
            const string sql = "SELECT * FROM CATALOG";
            var entities = await conn.QueryAsync<StockEntity>(sql);

            if (force) _memoryCache.Clear();

            foreach (var entity in entities)
            {
                _memoryCache[entity.Ticker] = entity.ToModel();
            }

            _isCacheLoaded = true;
            _logger.LogInformation("Loaded {Count} stocks from Firebird to memory cache.", _memoryCache.Count);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task LoadCountryToMemoryAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT * FROM CATALOG WHERE COUNTRY = @Country";
        var entities = await conn.QueryAsync<StockEntity>(sql, new { Country = countryCode });

        int count = 0;
        foreach (var entity in entities)
        {
            _memoryCache[entity.Ticker] = entity.ToModel();
            count++;
        }

        _logger.LogInformation("Loaded {Count} stocks for {Country} from Firebird to memory.", count, countryCode);
        await _systemLogger.LogCatalogAsync("Stock Catalog", $"Loaded {count} stocks for {countryCode} into memory.");
    }

    public async Task SetLastSyncDateAsync(CountryCode country, DateTime date)
    {
        using var conn = CreateConnection();
        const string sql = @"
            UPDATE OR INSERT INTO CATALOG_METADATA (KEY_NAME, VALUE_TEXT, UPDATED_AT)
            VALUES (@Key, @Value, CURRENT_TIMESTAMP)
            MATCHING (KEY_NAME)";

        await conn.ExecuteAsync(sql, new { Key = $"lastsync:{country}", Value = date.ToString("yyyy-MM-dd") });
    }

    public async Task<DateTime?> GetLastSyncDateAsync(CountryCode country)
    {
        using var conn = CreateConnection();
        const string sql = "SELECT VALUE_TEXT FROM CATALOG_METADATA WHERE KEY_NAME = @Key";
        var val = await conn.ExecuteScalarAsync<string>(sql, new { Key = $"lastsync:{country}" });

        if (DateTime.TryParse(val, out var date)) return date;
        return null;
    }

    public async Task<string[]> GetActiveCountriesAsync()
    {
        using var conn = CreateConnection();
        const string sql = "SELECT DISTINCT COUNTRY FROM CATALOG";
        var countries = await conn.QueryAsync<string>(sql);
        return countries.ToArray();
    }

    private string GetCountryByExchange(string exchange)
    {
        return exchange.ToUpper() switch
        {
            "KOSPI" or "KOSDAQ" => "KR",
            "NASDAQ" or "NYSE" or "AMEX" => "US",
            "HKEX" => "HK",
            "SSE" or "SZSE" => "CN",
            "HOSE" or "HNX" => "VN",
            "TSE" => "JP",
            _ => "Unknown"
        };
    }

    private class StockEntity
    {
        public string Ticker { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Exchange { get; set; } = null!;
        public string Currency { get; set; } = null!;
        public string Country { get; set; } = null!;

        public Stock ToModel() => new()
        {
            Ticker = Ticker,
            Name = Name,
            Exchange = Exchange,
            Currency = Enum.Parse<CurrencyType>(Currency)
        };
    }
}
