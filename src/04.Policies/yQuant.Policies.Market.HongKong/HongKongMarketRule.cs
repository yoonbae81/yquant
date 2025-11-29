using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Models;

namespace yQuant.Policies.Market.HongKong;

public class HongKongMarketRule : IMarketRule
{
    private readonly TimeZoneInfo _hongKongZone;

    public HongKongMarketRule()
    {
        // Hong Kong Time (UTC+8)
        // Windows ID: "China Standard Time"
        try 
        {
            _hongKongZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            _hongKongZone = TimeZoneInfo.CreateCustomTimeZone("China Standard Time", new TimeSpan(8, 0, 0), "China Standard Time", "China Standard Time");
        }
    }

    public bool CanHandle(string exchange) =>
        new[] { "HKEX" }.Contains(exchange.ToUpper());

    public bool IsMarketOpen(DateTime timestamp)
    {
        var hkt = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _hongKongZone);

        // Check if it's a weekday (Monday to Friday)
        if (hkt.DayOfWeek == DayOfWeek.Saturday || hkt.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Trading hours:
        // Morning: 09:30 - 12:00
        // Afternoon: 13:00 - 16:00
        
        var time = hkt.TimeOfDay;
        var morningOpen = new TimeSpan(9, 30, 0);
        var morningClose = new TimeSpan(12, 0, 0);
        var afternoonOpen = new TimeSpan(13, 0, 0);
        var afternoonClose = new TimeSpan(16, 0, 0);

        bool isMorningSession = time >= morningOpen && time <= morningClose;
        bool isAfternoonSession = time >= afternoonOpen && time <= afternoonClose;

        return isMorningSession || isAfternoonSession;
    }

    public CurrencyType GetCurrency() => CurrencyType.HKD;
}
