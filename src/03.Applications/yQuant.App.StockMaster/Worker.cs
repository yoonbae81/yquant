using Microsoft.Extensions.Options;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.StockMaster
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IMasterDataSyncService _syncService;
        private readonly StockMasterSettings _settings;
        private readonly CommandLineArgs _args;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public Worker(
            ILogger<Worker> logger, 
            IMasterDataSyncService syncService,
            IOptions<StockMasterSettings> settings, 
            CommandLineArgs args,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger;
            _syncService = syncService;
            _settings = settings.Value;
            _args = args;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_args.Args.Contains("--worker"))
            {
                await RunWorkerModeAsync(stoppingToken);
            }
            else
            {
                await RunCliModeAsync(stoppingToken);
            }
        }

        private async Task RunWorkerModeAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StockMaster started in WORKER mode.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = DateTime.MaxValue;
                CountryCode? nextCountry = null;

                foreach (var country in _settings.Countries)
                {
                    if (!Enum.TryParse<CountryCode>(country.Key, true, out var countryCode))
                    {
                        _logger.LogWarning("Invalid country code in settings: {Country}", country.Key);
                        continue;
                    }
                    if (!TimeSpan.TryParse(country.Value.RunTime, out var runTime))
                    {
                        _logger.LogWarning("Invalid RunTime format for {Country}: {RunTime}", country.Key, country.Value.RunTime);
                        continue;
                    }

                    var countryNextRun = now.Date.Add(runTime);
                    
                    if (now >= countryNextRun)
                    {
                        countryNextRun = countryNextRun.AddDays(1);
                    }

                    if (countryNextRun < nextRun)
                    {
                        nextRun = countryNextRun;
                        nextCountry = countryCode;
                    }
                }

                if (nextCountry == null)
                {
                    _logger.LogWarning("No valid schedules found. Worker will sleep for 1 hour.");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    continue;
                }

                var delay = nextRun - now;
                _logger.LogInformation("Next run scheduled for {Country} at {NextRun}", nextCountry, nextRun);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (stoppingToken.IsCancellationRequested) break;

                await RunJobAsync(nextCountry.Value, _settings.Countries[nextCountry.Value.ToString()], stoppingToken);
            }
        }

        private async Task RunCliModeAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StockMaster started in CLI mode.");

            try
            {
                if (_args.Args.Length == 0)
                {
                    _logger.LogInformation("No arguments provided. Updating ALL countries.");
                    
                    var allCountries = new Dictionary<CountryCode, Dictionary<string, string>>();
                    foreach (var c in _settings.Countries)
                    {
                        if (Enum.TryParse<CountryCode>(c.Key, true, out var code))
                        {
                            allCountries[code] = c.Value.Exchanges;
                        }
                    }
                    
                    await _syncService.SyncAllAsync(allCountries, stoppingToken);
                }
                else
                {
                    var targetCountry = _args.Args[0];
                    _logger.LogInformation("Target country provided: {Country}", targetCountry);

                    var countryEntry = _settings.Countries.FirstOrDefault(c => c.Key.Equals(targetCountry, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(countryEntry.Key) && Enum.TryParse<CountryCode>(countryEntry.Key, true, out var targetCode))
                    {
                        await RunJobAsync(targetCode, countryEntry.Value, stoppingToken);
                    }
                    else
                    {
                        _logger.LogError("Country '{Country}' not found in configuration.", targetCountry);
                        _logger.LogInformation("Available countries: {Countries}", string.Join(", ", _settings.Countries.Keys));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during execution.");
            }
            finally
            {
                _logger.LogInformation("StockMaster work completed. Shutting down.");
                _hostApplicationLifetime.StopApplication();
            }
        }

        private async Task RunJobAsync(CountryCode countryName, CountrySetting setting, CancellationToken stoppingToken)
        {
            await _syncService.SyncCountryAsync(countryName, setting.Exchanges, stoppingToken);
        }
    }
}
