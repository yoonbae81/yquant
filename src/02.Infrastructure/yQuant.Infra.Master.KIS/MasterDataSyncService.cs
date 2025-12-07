using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Master.KIS
{
    /// <summary>
    /// Service for synchronizing stock master data from external sources.
    /// </summary>
    public class MasterDataSyncService : IMasterDataSyncService
    {
        private readonly IMasterDataLoader _loader;
        private readonly IMasterDataRepository _repository;
        private readonly ISystemLogger _systemLogger;
        private readonly ILogger<MasterDataSyncService> _logger;

        public MasterDataSyncService(
            IMasterDataLoader loader,
            IMasterDataRepository repository,
            ISystemLogger systemLogger,
            ILogger<MasterDataSyncService> logger)
        {
            _loader = loader;
            _repository = repository;
            _systemLogger = systemLogger;
            _logger = logger;
        }

        public async Task SyncCountryAsync(CountryCode country, Dictionary<string, string> exchangeUrls, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting StockMaster Sync Job for {Country}...", country);

            try
            {
                // Load data from all exchanges in parallel
                var tasks = exchangeUrls
                    .Where(e => !string.IsNullOrEmpty(e.Value))
                    .Select(e => _loader.LoadMasterDataAsync(e.Key, e.Value))
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
                        $"StockMaster Sync Failed ({country})",
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

                    _logger.LogInformation("[{Country}] StockMaster Sync Completed Successfully (Repository Updated).", country);

                    // Additional: Save Korean domestic tickers to file for Redis-independent classification
                    if (country == CountryCode.KR)
                    {
                        await SaveDomesticTickersToFileAsync(allStocks);
                    }

                    var breakdown = string.Join("\n", allStocks.GroupBy(s => s.Exchange).Select(g => $"- {g.Key}: {g.Count()} items"));
                    await _systemLogger.LogStatusAsync("StockMaster", $"Sync Completed for {country}.\n{breakdown}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Country}] Failed to save to repository.", country);
                    await _systemLogger.LogSystemErrorAsync($"StockMaster Save Failed ({country})", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during StockMaster Sync Job for {Country}", country);
                await _systemLogger.LogSystemErrorAsync($"StockMaster Sync Job ({country})", ex);
            }
        }

        public async Task SyncAllAsync(Dictionary<CountryCode, Dictionary<string, string>> countries, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting sync for all {Count} countries", countries.Count);

            foreach (var country in countries)
            {
                await SyncCountryAsync(country.Key, country.Value, cancellationToken);
            }

            _logger.LogInformation("Completed sync for all countries");
        }

        /// <summary>
        /// Saves Korean domestic tickers to a plain text file for Redis-independent classification.
        /// File is saved to system temp directory for cross-application access.
        /// </summary>
        private async Task SaveDomesticTickersToFileAsync(IEnumerable<StockMaster> stocks)
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
}
