using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class MariaDbStockCatalogRepository : IStockCatalogRepository
{
    private readonly MariaDbContext _context;
    private readonly ILogger<MariaDbStockCatalogRepository> _logger;
    private readonly ISystemLogger _systemLogger;

    private static readonly ConcurrentDictionary<string, Stock> _memoryCache = new();
    private static bool _isCacheLoaded = false;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public MariaDbStockCatalogRepository(
        MariaDbContext context,
        ILogger<MariaDbStockCatalogRepository> logger,
        ISystemLogger systemLogger)
    {
        _context = context;
        _logger = logger;
        _systemLogger = systemLogger;
    }

    public async Task SaveBatchAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken = default)
    {
        var stockList = stocks.ToList();
        foreach (var stock in stockList)
        {
            var country = GetCountryByExchange(stock.Exchange);
            var entity = new StockCatalogEntity
            {
                Ticker = stock.Ticker,
                Name = stock.Name,
                Exchange = stock.Exchange,
                Currency = stock.Currency.ToString(),
                Country = country,
                LastUpdated = DateTime.UtcNow
            };

            var existing = await _context.Catalog.FindAsync(stock.Ticker);
            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(entity);
            }
            else
            {
                await _context.Catalog.AddAsync(entity, cancellationToken);
            }

            _memoryCache[stock.Ticker] = stock;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved {Count} stocks to MariaDB Stock Catalog.", stockList.Count);
    }

    public async Task<Stock?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(ticker, out var cached)) return cached;

        var entity = await _context.Catalog.FindAsync(new object[] { ticker }, cancellationToken);
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

            var entities = await _context.Catalog.ToListAsync(cancellationToken);

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
        var entities = await _context.Catalog
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
        var key = $"lastsync:{country}";
        var entity = await _context.CatalogMetadata.FindAsync(key);

        if (entity != null)
        {
            entity.ValueText = date.ToString("yyyy-MM-dd");
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await _context.CatalogMetadata.AddAsync(new CatalogMetadataEntity
            {
                KeyName = key,
                ValueText = date.ToString("yyyy-MM-dd"),
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<DateTime?> GetLastSyncDateAsync(CountryCode country)
    {
        var key = $"lastsync:{country}";
        var entity = await _context.CatalogMetadata.FindAsync(key);

        if (entity != null && DateTime.TryParse(entity.ValueText, out var date))
        {
            return date;
        }

        return null;
    }

    public async Task<string[]> GetActiveCountriesAsync()
    {
        var countries = await _context.Catalog
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
