using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Valkey.Interfaces;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Valkey.Services;

/// <summary>
/// Background service that listens for catalog update events and triggers a memory cache reload.
/// </summary>
public class CatalogUpdateSubscriber : BackgroundService
{
    private readonly IValkeyService _messageValkey;
    private readonly IStockCatalogRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogUpdateSubscriber> _logger;

    public CatalogUpdateSubscriber(
        IValkeyService messageValkey,
        IStockCatalogRepository repository,
        IConfiguration configuration,
        ILogger<CatalogUpdateSubscriber> logger)
    {
        _messageValkey = messageValkey;
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CatalogUpdateSubscriber starting...");

        // Background Warm-up: Load all countries found in Valkey
        _ = Task.Run(async () =>
        {
            try
            {
                var countries = await _repository.GetActiveCountriesAsync();
                if (countries.Any())
                {
                    // Sort countries by market priority (Open > Opening Soon > Closed recently > Others)
                    var prioritizedCountries = countries
                        .Select(c => new { Code = c, Priority = GetMarketPriority(c) })
                        .OrderBy(x => x.Priority)
                        .Select(x => x.Code)
                        .ToList();

                    _logger.LogInformation("Background warm-up: Found {Count} countries. Priority order: {Order}",
                        countries.Length, string.Join(" > ", prioritizedCountries));

                    // Load sequentially by priority
                    foreach (var country in prioritizedCountries)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        await _repository.LoadCountryToMemoryAsync(country, stoppingToken);
                    }
                    _logger.LogInformation("Priority background warm-up for all countries completed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to perform initial background warm-up.");
            }
        }, stoppingToken);

        var subscriber = _messageValkey.Connection.GetSubscriber();

        await subscriber.SubscribeAsync(RedisChannel.Literal("catalog:events"), async (channel, message) =>
        {
            var msgStr = message.ToString();
            if (msgStr == "catalog:updated")
            {
                _logger.LogInformation("Received 'catalog:updated' event. Triggering full memory cache reload...");
                try
                {
                    await _repository.LoadAllToMemoryAsync(force: true, cancellationToken: stoppingToken);
                    _logger.LogInformation("Full memory cache reload completed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload full memory cache.");
                }
            }
            else if (msgStr.StartsWith("catalog:updated:"))
            {
                var country = msgStr.Replace("catalog:updated:", "");
                _logger.LogInformation("Received 'catalog:updated:{Country}' event. Triggering partial memory cache reload...", country);
                try
                {
                    await _repository.LoadAllToMemoryAsync(force: false, countryCode: country, cancellationToken: stoppingToken);
                    _logger.LogInformation("Partial memory cache reload for {Country} completed successfully.", country);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload memory cache for {Country}.", country);
                }
            }
        });

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        await subscriber.UnsubscribeAsync(RedisChannel.Literal("catalog:events"));
        _logger.LogInformation("CatalogUpdateSubscriber stopped.");
    }

    private int GetMarketPriority(string country)
    {
        try
        {
            var marketConfig = _configuration.GetSection($"Markets:{country.ToUpper()}");
            if (!marketConfig.Exists()) return 999;

            var timeZoneId = marketConfig.GetValue<string>("TimeZone") ?? "UTC";
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var currentTime = nowLocal.TimeOfDay;

            // Try to find any open/close times to determine if it's currently relevant
            var tradingHours = marketConfig.GetSection("TradingHours");

            // Simplified logic: Check if within Regular Hours or within 2 hours of opening
            TimeSpan? openTime = null;
            TimeSpan? closeTime = null;

            if (tradingHours.Exists())
            {
                // Check multiple possible key names (Open, RegularOpen, MorningOpen)
                var openStr = tradingHours["Open"] ?? tradingHours["RegularOpen"] ?? tradingHours["MorningOpen"];
                var closeStr = tradingHours["Close"] ?? tradingHours["RegularClose"] ?? tradingHours["AfternoonClose"];

                if (TimeSpan.TryParse(openStr, out var ot)) openTime = ot;
                if (TimeSpan.TryParse(closeStr, out var ct)) closeTime = ct;
            }

            if (openTime.HasValue && closeTime.HasValue)
            {
                // 1. Currently Open
                if (currentTime >= openTime && currentTime <= closeTime) return 0;

                // 2. Opening soon (within 2 hours)
                if (currentTime < openTime && (openTime.Value - currentTime).TotalHours <= 2) return 1;

                // 3. Just closed (within 1 hour)
                if (currentTime > closeTime && (currentTime - closeTime.Value).TotalHours <= 1) return 2;

                // 4. Opening later today
                if (currentTime < openTime) return 3;
            }

            return 10; // Default low priority
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to calculate priority for {Country}", country);
            return 100;
        }
    }
}
