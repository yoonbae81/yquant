using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Policies;

namespace yQuant.Core.Policies;

public class ConfigurableMarketRule : IMarketRule
{
    private readonly string[] _exchanges;
    private readonly TimeZoneInfo _timeZone;
    private readonly CurrencyType _currency;
    private readonly MarketTradingHours _tradingHours;

    public ConfigurableMarketRule(IConfiguration marketConfig)
    {
        // Read exchanges
        _exchanges = marketConfig.GetSection("Exchanges").Get<string[]>()
            ?? throw new ArgumentException("Exchanges configuration is missing");

        // Read timezone
        var timeZone = marketConfig["TimeZone"]
            ?? throw new ArgumentException("TimeZone configuration is missing");

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback for cross-platform compatibility
            var offset = timeZone switch
            {
                "Korea Standard Time" or "Asia/Seoul" => new TimeSpan(9, 0, 0),
                "Eastern Standard Time" or "America/New_York" => new TimeSpan(-5, 0, 0),
                "China Standard Time" or "Asia/Shanghai" or "Asia/Hong_Kong" => new TimeSpan(8, 0, 0),
                "Tokyo Standard Time" or "Asia/Tokyo" => new TimeSpan(9, 0, 0),
                "SE Asia Standard Time" or "Asia/Ho_Chi_Minh" => new TimeSpan(7, 0, 0),
                _ => throw new ArgumentException($"Unknown timezone: {timeZone}")
            };
            _timeZone = TimeZoneInfo.CreateCustomTimeZone(timeZone, offset, timeZone, timeZone);
        }

        // Read currency
        var currencyStr = marketConfig["Currency"]
            ?? throw new ArgumentException("Currency configuration is missing");
        _currency = Enum.Parse<CurrencyType>(currencyStr);

        // Read trading hours
        _tradingHours = ParseTradingHours(marketConfig.GetSection("TradingHours"));
    }

    public bool CanHandle(string exchange) =>
        _exchanges.Contains(exchange, StringComparer.OrdinalIgnoreCase);

    public bool IsMarketOpen(DateTime timestamp)
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _timeZone);

        // Check if it's a weekday (Monday to Friday)
        if (localTime.DayOfWeek == DayOfWeek.Saturday || localTime.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        var time = localTime.TimeOfDay;

        // Check based on market type
        if (_tradingHours.HasMorningSessions)
        {
            // Markets with morning/afternoon sessions (China, Japan, HongKong, Vietnam)
            bool isMorningSession = time >= _tradingHours.MorningOpen && time <= _tradingHours.MorningClose;
            bool isAfternoonSession = time >= _tradingHours.AfternoonOpen && time <= _tradingHours.AfternoonClose;
            return isMorningSession || isAfternoonSession;
        }
        else if (_tradingHours.HasPreMarket)
        {
            // US markets with pre-market option
            if (time >= _tradingHours.RegularOpen && time < _tradingHours.RegularClose)
            {
                return true;
            }
            if (_tradingHours.AllowPreMarket && time >= _tradingHours.PreMarketOpen && time < _tradingHours.RegularOpen)
            {
                return true;
            }
            return false;
        }
        else
        {
            // Simple open/close markets (Korea)
            return time >= _tradingHours.Open && time <= _tradingHours.Close;
        }
    }

    public CurrencyType GetCurrency() => _currency;

    private static MarketTradingHours ParseTradingHours(IConfigurationSection config)
    {
        var hours = new MarketTradingHours();

        // Check for simple open/close
        if (config["Open"] != null && config["Close"] != null)
        {
            hours.Open = TimeSpan.Parse(config["Open"]!);
            hours.Close = TimeSpan.Parse(config["Close"]!);
            return hours;
        }

        // Check for US-style regular/pre-market
        if (config["RegularOpen"] != null && config["RegularClose"] != null)
        {
            hours.HasPreMarket = true;
            hours.RegularOpen = TimeSpan.Parse(config["RegularOpen"]!);
            hours.RegularClose = TimeSpan.Parse(config["RegularClose"]!);

            if (config["PreMarketOpen"] != null)
            {
                hours.PreMarketOpen = TimeSpan.Parse(config["PreMarketOpen"]!);
            }

            if (config["AllowPreMarket"] != null)
            {
                hours.AllowPreMarket = bool.Parse(config["AllowPreMarket"]!);
            }

            return hours;
        }

        // Check for morning/afternoon sessions
        if (config["MorningOpen"] != null && config["AfternoonClose"] != null)
        {
            hours.HasMorningSessions = true;
            hours.MorningOpen = TimeSpan.Parse(config["MorningOpen"]!);
            hours.MorningClose = TimeSpan.Parse(config["MorningClose"]!);
            hours.AfternoonOpen = TimeSpan.Parse(config["AfternoonOpen"]!);
            hours.AfternoonClose = TimeSpan.Parse(config["AfternoonClose"]!);
            return hours;
        }

        throw new ArgumentException("Invalid trading hours configuration");
    }

    private class MarketTradingHours
    {
        // Simple open/close (Korea)
        public TimeSpan Open { get; set; }
        public TimeSpan Close { get; set; }

        // US-style regular + pre-market
        public bool HasPreMarket { get; set; }
        public TimeSpan RegularOpen { get; set; }
        public TimeSpan RegularClose { get; set; }
        public TimeSpan PreMarketOpen { get; set; }
        public bool AllowPreMarket { get; set; }

        // Morning/Afternoon sessions (China, Japan, HongKong, Vietnam)
        public bool HasMorningSessions { get; set; }
        public TimeSpan MorningOpen { get; set; }
        public TimeSpan MorningClose { get; set; }
        public TimeSpan AfternoonOpen { get; set; }
        public TimeSpan AfternoonClose { get; set; }
    }
}
