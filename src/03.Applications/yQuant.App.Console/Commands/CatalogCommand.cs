using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using yQuant.Core.Models;
using yQuant.App.Console.Services;

namespace yQuant.App.Console.Commands
{
    public class CatalogCommand : ICommand
    {
        private readonly StockCatalogSyncService _syncService;
        private readonly ILogger<CatalogCommand> _logger;
        private readonly CatalogSettings _settings;

        public string Name => "catalog";
        public string Description => "Sync stock catalog data for specified country or exchange (or all if no argument)";

        public CatalogCommand(
            StockCatalogSyncService syncService,
            ILogger<CatalogCommand> logger,
            IOptions<CatalogSettings> settings)
        {
            _syncService = syncService;
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task ExecuteAsync(string[] args)
        {
            try
            {
                // args[0] is "catalog", args[1] would be the country or exchange (optional)
                if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    _logger.LogInformation("No target specified. Syncing ALL exchanges.");
                    await SyncAllExchangesAsync();
                }
                else
                {
                    var target = args[1].ToUpper();

                    // Check if it's an exchange name
                    if (_settings.Exchanges.ContainsKey(target))
                    {
                        await SyncExchangeAsync(target);
                    }
                    // Otherwise treat it as a country code
                    else
                    {
                        await SyncCountryAsync(target);
                    }
                }

                System.Console.WriteLine("Stock catalog data sync completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync stock catalog data");
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task SyncAllExchangesAsync()
        {
            var exchangesByCountry = _settings.Exchanges
                .GroupBy(e => e.Value.Country)
                .ToDictionary(
                    g => Enum.Parse<CountryCode>(g.Key, true),
                    g => g.ToDictionary(e => e.Key, e => e.Value.URL)
                );

            foreach (var country in exchangesByCountry.Keys)
            {
                var exchanges = exchangesByCountry[country];
                System.Console.WriteLine($"Queued: {country} ({string.Join(", ", exchanges.Keys)})");
            }

            await _syncService.SyncAllAsync(exchangesByCountry, CancellationToken.None);
        }

        private async Task SyncCountryAsync(string countryCode)
        {
            var exchanges = _settings.Exchanges
                .Where(e => e.Value.Country.Equals(countryCode, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(e => e.Key, e => e.Value.URL);

            if (!exchanges.Any())
            {
                System.Console.WriteLine($"Country '{countryCode}' not found in configuration.");
                var availableCountries = _settings.Exchanges
                    .Select(e => e.Value.Country)
                    .Distinct()
                    .OrderBy(c => c);
                System.Console.WriteLine($"Available countries: {string.Join(", ", availableCountries)}");
                return;
            }

            if (!Enum.TryParse<CountryCode>(countryCode, true, out var targetCode))
            {
                System.Console.WriteLine($"Invalid country code: {countryCode}");
                return;
            }

            System.Console.WriteLine($"Syncing {countryCode} ({string.Join(", ", exchanges.Keys)})...");
            await _syncService.SyncCountryAsync(targetCode, exchanges, CancellationToken.None);
        }

        private async Task SyncExchangeAsync(string exchangeName)
        {
            if (!_settings.Exchanges.TryGetValue(exchangeName, out var exchange))
            {
                System.Console.WriteLine($"Exchange '{exchangeName}' not found in configuration.");
                System.Console.WriteLine($"Available exchanges: {string.Join(", ", _settings.Exchanges.Keys)}");
                return;
            }

            if (!Enum.TryParse<CountryCode>(exchange.Country, true, out var countryCode))
            {
                System.Console.WriteLine($"Invalid country code for exchange {exchangeName}: {exchange.Country}");
                return;
            }

            var exchanges = new Dictionary<string, string>
            {
                { exchangeName, exchange.URL }
            };

            System.Console.WriteLine($"Syncing {exchangeName} ({exchange.Country})...");
            await _syncService.SyncCountryAsync(countryCode, exchanges, CancellationToken.None);
        }
    }

    public class CatalogSettings
    {
        public Dictionary<string, ExchangeSetting> Exchanges { get; set; } = new();
    }

    public class ExchangeSetting
    {
        public string Country { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
        public string RunTime { get; set; } = "07:00:00";
    }
}
