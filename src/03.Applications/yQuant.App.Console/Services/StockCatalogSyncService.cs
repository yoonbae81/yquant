using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Valkey.Services;

namespace yQuant.App.Console.Services;

/// <summary>
/// Service for synchronizing stock catalog data from external sources.
/// </summary>
public class StockCatalogSyncService
{
    private readonly StockCatalogLoader _loader;
    private readonly IStockCatalogRepository _repository;
    private readonly ISystemLogger _systemLogger;
    private readonly IValkeyService _messageValkey;
    private readonly ILogger<StockCatalogSyncService> _logger;

    public StockCatalogSyncService(
        StockCatalogLoader loader,
        IStockCatalogRepository repository,
        ISystemLogger systemLogger,
        IValkeyService messageValkey,
        ILogger<StockCatalogSyncService> logger)
    {
        _loader = loader;
        _repository = repository;
        _systemLogger = systemLogger;
        _messageValkey = messageValkey;
        _logger = logger;
    }

    public async Task SyncCountryAsync(CountryCode country, Dictionary<string, string> exchangeUrls, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Stock Catalog Sync for {Country}...", country);

        try
        {
            // Load data from all exchanges in parallel
            var tasks = exchangeUrls
                .Where(e => !string.IsNullOrEmpty(e.Value))
                .Select(e => _loader.LoadCatalogAsync(e.Key, e.Value))
                .ToList();

            if (tasks.Count == 0)
            {
                _logger.LogInformation("No URLs configured for {Country}. Skipping.", country);
                return;
            }

            await Task.WhenAll(tasks);

            // Merge results
            var allStocks = tasks.SelectMany(t => t.Result).ToList();

            _logger.LogInformation("[{Country}] Loaded {Count} stocks total.", country, allStocks.Count);

            // Check if no stocks were loaded - this indicates a failure
            if (allStocks.Count == 0)
            {
                _logger.LogError("[{Country}] No stocks were loaded. This indicates a download or parsing failure.", country);
                await _systemLogger.LogSystemErrorAsync(
                    $"Stock Catalog Sync Failed ({country})",
                    new InvalidOperationException($"Failed to load any stocks for {country}. Check if URLs are valid and accessible."));
                return;
            }

            // Log breakdown by exchange
            foreach (var group in allStocks.GroupBy(s => s.Exchange))
            {
                _logger.LogInformation("Exchange {Exchange}: {Count} stocks", group.Key, group.Count());
            }

            // Sample output - Log 5 random stocks
            var random = new Random();
            var samples = allStocks.OrderBy(x => random.Next()).Take(5).ToList();

            _logger.LogInformation("--- Sample 5 Stocks Fetched for {Country} ---", country);
            foreach (var s in samples)
            {
                _logger.LogInformation("[{Exchange}] {Ticker} : {Name} ({Currency})",
                    s.Exchange, s.Ticker, s.Name, s.Currency);
            }
            _logger.LogInformation("--------------------------------");

            // Save to repository
            try
            {
                await _repository.SaveBatchAsync(allStocks, cancellationToken);

                // Track this country in the global active list
                var db = _messageValkey.Connection.GetDatabase(); // Use Message Valkey for discovery
                await db.SetAddAsync("catalog:countries", country.ToString());

                _logger.LogInformation("[{Country}] Stock Catalog Sync Completed Successfully (Repository Updated).", country);

                // Additional: Save Korean domestic tickers to file for Valkey-independent classification
                if (country == CountryCode.KR)
                {
                    await SaveDomesticTickersToFileAsync(allStocks);
                }

                await _repository.SetLastSyncDateAsync(country, DateTime.UtcNow);

                var breakdown = string.Join("\n", allStocks.GroupBy(s => s.Exchange).Select(g => $"- {g.Key}: {g.Count()} items"));
                await _systemLogger.LogCatalogAsync("Stock Catalog", $"Sync Completed for {country}.\n{breakdown}");

                // Notify app servers to reload their memory cache for this country
                await NotifyUpdateAsync(country);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Country}] Failed to save to repository.", country);
                await _systemLogger.LogSystemErrorAsync($"Stock Catalog Save Failed ({country})", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Stock Catalog Sync for {Country}", country);
            await _systemLogger.LogSystemErrorAsync($"Stock Catalog Sync ({country})", ex);
        }
    }

    public async Task SyncAllAsync(Dictionary<CountryCode, Dictionary<string, string>> countries, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting catalog sync for all {Count} countries", countries.Count);

        foreach (var country in countries)
        {
            await SyncCountryAsync(country.Key, country.Value, cancellationToken);
        }

        _logger.LogInformation("Completed catalog sync for all countries");
    }

    private async Task NotifyUpdateAsync(CountryCode? country = null)
    {
        try
        {
            var eventName = country.HasValue ? $"catalog:updated:{country.Value}" : "catalog:updated";
            var subscriber = _messageValkey.Connection.GetSubscriber();
            await subscriber.PublishAsync(StackExchange.Redis.RedisChannel.Literal("catalog:events"), eventName);
            _logger.LogInformation("Published '{EventName}' event to Message Valkey.", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish catalog update event (non-critical).");
        }
    }

    /// <summary>
    /// Saves Korean domestic tickers to a plain text file for Valkey-independent classification.
    /// File is saved to system temp directory for cross-application access.
    /// </summary>
    private async Task SaveDomesticTickersToFileAsync(IEnumerable<Stock> stocks)
    {
        try
        {
            var tempDir = Path.GetTempPath();
            var filePath = Path.Combine(tempDir, "domestic_tickers.txt");

            var tickers = stocks
                .Select(s => s.Ticker)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .OrderBy(t => t)
                .Distinct();

            // Atomic write: write to temp file first, then move
            var tempFile = $"{filePath}.tmp";
            await File.WriteAllLinesAsync(tempFile, tickers);
            File.Move(tempFile, filePath, overwrite: true);

            _logger.LogInformation("Saved {Count} domestic tickers to {File}", tickers.Count(), filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save domestic tickers to file (non-critical)");
        }
    }
}
