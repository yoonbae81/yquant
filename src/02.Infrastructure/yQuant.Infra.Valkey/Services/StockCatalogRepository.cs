using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Valkey.Interfaces;

namespace yQuant.Infra.Valkey.Services;

/// <summary>
/// Valkey-based repository for stock catalog data.
/// </summary>
public class StockCatalogRepository
{
    private readonly IStorageValkeyService _storageService;
    private readonly ILogger<StockCatalogRepository> _logger;
    private readonly ISystemLogger _systemLogger;
    private const string KeyPrefix = "stock:";
    private const string CountrySetPrefix = "catalog:country:";
    private const string LastSyncPrefix = "catalog:lastsync:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(25);

    // In-memory cache for fast access in Blue/Green servers
    private static readonly ConcurrentDictionary<string, Stock> _memoryCache = new();
    private static bool _isCacheLoaded = false;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public StockCatalogRepository(
        IStorageValkeyService storageService,
        ILogger<StockCatalogRepository> logger,
        ISystemLogger systemLogger)
    {
        _storageService = storageService;
        _logger = logger;
        _systemLogger = systemLogger;
    }

    public async Task SaveBatchAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken = default)
    {
        const int ChunkSize = 500;
        var stockList = stocks.ToList();
        if (stockList.Count == 0) return;

        var db = _storageService.Connection.GetDatabase();

        for (int i = 0; i < stockList.Count; i += ChunkSize)
        {
            var chunk = stockList.Skip(i).Take(ChunkSize);
            var batch = db.CreateBatch();
            var tasks = new List<Task>();

            foreach (var stock in chunk)
            {
                var key = $"{KeyPrefix}{stock.Ticker}";
                tasks.Add(batch.HashSetAsync(key, new HashEntry[]
                {
                    new("name", stock.Name),
                    new("exchange", stock.Exchange),
                    new("currency", stock.Currency.ToString()),
                    new("country", stock.Exchange == "KOSPI" || stock.Exchange == "KOSDAQ" ? "KR" : "Unknown") // Need better country detection
                }));

                // Add to country-specific set for granular reload
                var countryCode = GetCountryByExchange(stock.Exchange);
                tasks.Add(batch.SetAddAsync($"{CountrySetPrefix}{countryCode}", stock.Ticker));

                tasks.Add(batch.KeyExpireAsync(key, DefaultExpiration));
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Saved chunk {Start}-{End} of {Total} stocks to Valkey", i, Math.Min(i + ChunkSize, stockList.Count), stockList.Count);
        }

        _logger.LogInformation("Successfully saved {Count} stocks to Storage Valkey in chunks.", stockList.Count);

        // Update memory cache after saving to Valkey
        foreach (var stock in stockList)
        {
            _memoryCache[stock.Ticker] = stock;
        }
    }

    public async Task<Stock?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        // 1. Check memory cache first
        if (_memoryCache.TryGetValue(ticker, out var cachedStock))
        {
            return cachedStock;
        }

        // 2. Fallback: Direct Valkey lookup (JIT Loading)
        try
        {
            var db = _storageService.Connection.GetDatabase();
            var key = $"{KeyPrefix}{ticker}";

            var entries = await db.HashGetAllAsync(key);
            if (entries.Length == 0) return null;

            var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

            var stock = new Stock
            {
                Ticker = ticker,
                Name = dict.GetValueOrDefault("name", ""),
                Exchange = dict.GetValueOrDefault("exchange", ""),
                Currency = Enum.TryParse<CurrencyType>(dict.GetValueOrDefault("currency", "USD"), out var currency)
                    ? currency
                    : CurrencyType.USD
            };

            // Cache it for future use
            _memoryCache[ticker] = stock;
            return stock;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch stock {Ticker} from Storage Valkey (fallback).", ticker);
            return null;
        }
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

            _logger.LogInformation(force ? "Force reloading all stock catalog data..." : "Starting background warmup of stock catalog memory cache...");

            if (force) _memoryCache.Clear();

            // Note: We don't perform Keys * anymore. 
            // Instead, we mark as loaded and let the BackgroundService or GetByTicker fill it.
            _isCacheLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize stock catalog cache.");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task LoadCountryToMemoryAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Warming up memory cache for {Country}...", countryCode);
            var db = _storageService.Connection.GetDatabase();
            var tickers = await db.SetMembersAsync($"{CountrySetPrefix}{countryCode}");

            int count = 0;
            foreach (var ticker in tickers)
            {
                if (cancellationToken.IsCancellationRequested) break;
                // GetByTickerAsync will fetch from Valkey and cache it in memory
                await GetByTickerAsync(ticker.ToString(), cancellationToken);
                count++;
            }

            _logger.LogInformation("Successfully warmed up {Count} stocks for {Country}.", count, countryCode);
            await _systemLogger.LogCatalogAsync("Stock Catalog Cache", $"Loaded {count} stocks for {countryCode} into memory.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm up memory cache for {Country}.", countryCode);
            await _systemLogger.LogSystemErrorAsync($"Stock Catalog Load Failed ({countryCode})", ex);
        }
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

    public async Task SetLastSyncDateAsync(CountryCode country, DateTime date)
    {
        var db = _storageService.Connection.GetDatabase();
        await db.StringSetAsync($"{LastSyncPrefix}{country}", date.ToString("yyyy-MM-dd"));
    }

    public async Task<DateTime?> GetLastSyncDateAsync(CountryCode country)
    {
        var db = _storageService.Connection.GetDatabase();
        var val = await db.StringGetAsync($"{LastSyncPrefix}{country}");
        if (val.HasValue && DateTime.TryParse(val.ToString(), out var date))
        {
            return date;
        }
        return null;
    }

    public async Task<string[]> GetActiveCountriesAsync()
    {
        try
        {
            var db = _storageService.Connection.GetDatabase();
            var countries = await db.SetMembersAsync("catalog:countries");
            return countries.Select(c => c.ToString()).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
