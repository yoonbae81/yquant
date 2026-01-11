using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class MariaDbStockCatalogRepository : IStockCatalogRepository
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MariaDbStockCatalogRepository> _logger;
    private readonly ISystemLogger _systemLogger;

    private static readonly ConcurrentDictionary<string, Stock> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _isCacheLoaded = false;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public MariaDbStockCatalogRepository(
        IServiceProvider serviceProvider,
        ILogger<MariaDbStockCatalogRepository> logger,
        ISystemLogger systemLogger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _systemLogger = systemLogger;
    }

    public async Task SaveBatchAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        // Use raw SQL for efficient Bulk UPSERT (INSERT ... ON DUPLICATE KEY UPDATE)
        const int batchSize = 1000;
        var stockList = stocks.ToList();
        var total = stockList.Count;

        for (int i = 0; i < total; i += batchSize)
        {
            var chunk = stockList.Skip(i).Take(batchSize).ToList();
            if (!chunk.Any()) continue;

            var placeholderBuilder = new System.Text.StringBuilder();
            var parameters = new List<object>();

            placeholderBuilder.Append("INSERT INTO catalog (Ticker, Name, Exchange, Currency, Country, LastUpdated) VALUES ");

            for (int k = 0; k < chunk.Count; k++)
            {
                if (k > 0) placeholderBuilder.Append(", ");
                // Use {N} syntax relative to the flattened parameter list
                // Values: TickerIdx, NameIdx, ...
                int baseP = k * 6;
                placeholderBuilder.Append($"({{{baseP}}}, {{{baseP + 1}}}, {{{baseP + 2}}}, {{{baseP + 3}}}, {{{baseP + 4}}}, {{{baseP + 5}}})");

                var stock = chunk[k];
                var country = GetCountryByExchange(stock.Exchange);

                parameters.Add(stock.Ticker);
                parameters.Add(stock.Name);
                parameters.Add(stock.Exchange);
                parameters.Add(stock.Currency.ToString());
                parameters.Add(country);
                parameters.Add(DateTime.UtcNow);

                _memoryCache[stock.Ticker] = stock;
            }

            placeholderBuilder.Append(" ON DUPLICATE KEY UPDATE Name=VALUES(Name), Exchange=VALUES(Exchange), Currency=VALUES(Currency), Country=VALUES(Country), LastUpdated=VALUES(LastUpdated)");

            await context.Database.ExecuteSqlRawAsync(placeholderBuilder.ToString(), parameters.ToArray(), cancellationToken);
        }

        _logger.LogInformation("Saved {Count} stocks to MariaDB Stock Catalog (Bulk UPSERT).", total);
    }

    public async Task<Stock?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(ticker, out var cached)) return cached;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        var entity = await context.Catalog.FindAsync(new object[] { ticker }, cancellationToken);
        if (entity == null) return null;

        var stock = entity.ToModel();
        _memoryCache[ticker] = stock;
        return stock;
    }

    public async Task<IEnumerable<Stock>> GetByTickersAsync(IEnumerable<string> tickers, CancellationToken cancellationToken = default)
    {
        var distinctTickers = tickers.Distinct().ToList();
        var result = new List<Stock>();
        var missingTickers = new List<string>();

        foreach (var ticker in distinctTickers)
        {
            if (_memoryCache.TryGetValue(ticker, out var cached))
            {
                result.Add(cached);
            }
            else
            {
                missingTickers.Add(ticker);
            }
        }

        if (missingTickers.Any())
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

            // Chunking to be safe with SQL limits (e.g. 1000 items)
            const int batchSize = 1000;
            for (int i = 0; i < missingTickers.Count; i += batchSize)
            {
                var batch = missingTickers.Skip(i).Take(batchSize).ToList();
                var entities = await context.Catalog
                    .Where(c => batch.Contains(c.Ticker))
                    .ToListAsync(cancellationToken);

                foreach (var entity in entities)
                {
                    var stock = entity.ToModel();
                    _memoryCache[entity.Ticker] = stock;
                    result.Add(stock);
                }
            }
        }

        return result;
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

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

            var entities = await context.Catalog.ToListAsync(cancellationToken);

            if (force) _memoryCache.Clear();

            foreach (var entity in entities)
            {
                _memoryCache[entity.Ticker] = entity.ToModel();
            }

            _isCacheLoaded = true;
            _logger.LogInformation("Loaded {Count} stocks from MariaDB to memory cache.", _memoryCache.Count);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task LoadCountryToMemoryAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        var entities = await context.Catalog
            .Where(c => c.Country == countryCode)
            .ToListAsync(cancellationToken);

        int count = 0;
        foreach (var entity in entities)
        {
            _memoryCache[entity.Ticker] = entity.ToModel();
            count++;
        }

        _logger.LogInformation("Loaded {Count} stocks for {Country} from MariaDB to memory.", count, countryCode);
        await _systemLogger.LogCatalogAsync("Stock Catalog", $"Loaded {count} stocks for {countryCode} into memory.");
    }

    public async Task SetLastSyncDateAsync(CountryCode country, DateTime date)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        var key = $"lastsync:{country}";
        var entity = await context.CatalogMetadata.FindAsync(key);

        if (entity != null)
        {
            entity.ValueText = date.ToString("yyyy-MM-dd");
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await context.CatalogMetadata.AddAsync(new CatalogMetadataEntity
            {
                KeyName = key,
                ValueText = date.ToString("yyyy-MM-dd"),
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task<DateTime?> GetLastSyncDateAsync(CountryCode country)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        var key = $"lastsync:{country}";
        var entity = await context.CatalogMetadata.FindAsync(key);

        if (entity != null && DateTime.TryParse(entity.ValueText, out var date))
        {
            return date;
        }

        return null;
    }

    public async Task<string[]> GetActiveCountriesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

        var countries = await context.Catalog
            .Select(c => c.Country)
            .Distinct()
            .ToArrayAsync();

        return countries;
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
}
