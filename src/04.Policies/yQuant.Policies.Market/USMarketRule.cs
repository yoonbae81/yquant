using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace yQuant.Policies.Market;

public class USMarketRule : IMarketRule
{
    private readonly ILogger<USMarketRule> _logger;
    private readonly TimeZoneInfo _easternStandardTime;
    private readonly USMarketRuleSettings _settings;

    public USMarketRule(ILogger<USMarketRule> logger, IConfiguration configuration)
    {
        _logger = logger;
        _easternStandardTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        _settings = new USMarketRuleSettings();
        configuration.Bind(_settings);
    }

    public bool CanHandle(string exchange) =>
        new[] { "NYSE", "NASDAQ", "AMEX" }.Contains(exchange.ToUpper());

    public bool IsMarketOpen(DateTime timestamp)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _easternStandardTime);

        // Check if it's a weekday (Monday to Friday)
        if (et.DayOfWeek == DayOfWeek.Saturday || et.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Regular trading hours (9:30 AM to 4:00 PM ET)
        var regularMarketOpen = new TimeSpan(9, 30, 0);
        var regularMarketClose = new TimeSpan(16, 0, 0);

        if (et.TimeOfDay >= regularMarketOpen && et.TimeOfDay < regularMarketClose)
        {
            return true;
        }

        // Pre-market hours if allowed (e.g., 4:00 AM to 9:30 AM ET)
        if (_settings.AllowPreMarket)
        {
            var preMarketOpen = new TimeSpan(4, 0, 0);
            if (et.TimeOfDay >= preMarketOpen && et.TimeOfDay < regularMarketOpen)
            {
                return true;
            }
        }

        // Note: Real-world implementation would also check for holidays.
        return false;
    }

    public CurrencyType GetCurrency() => CurrencyType.USD;

    private class USMarketRuleSettings
    {
        public bool AllowPreMarket { get; set; } = false;
    }
}
