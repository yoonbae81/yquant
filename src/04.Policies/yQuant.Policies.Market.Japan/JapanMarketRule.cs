using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Models;

namespace yQuant.Policies.Market.Japan;

public class JapanMarketRule : IMarketRule
{
    private readonly TimeZoneInfo _japanZone;

    public JapanMarketRule()
    {
        // Japan Standard Time (UTC+9)
        // Windows ID: "Tokyo Standard Time"
        try 
        {
            _japanZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            _japanZone = TimeZoneInfo.CreateCustomTimeZone("Tokyo Standard Time", new TimeSpan(9, 0, 0), "Tokyo Standard Time", "Tokyo Standard Time");
        }
    }

    public bool CanHandle(string exchange) =>
        new[] { "TSE" }.Contains(exchange.ToUpper());

    public bool IsMarketOpen(DateTime timestamp)
    {
        var jst = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _japanZone);

        // Check if it's a weekday (Monday to Friday)
        if (jst.DayOfWeek == DayOfWeek.Saturday || jst.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Trading hours:
        // Morning: 09:00 - 11:30
        // Afternoon: 12:30 - 15:00
        
        var time = jst.TimeOfDay;
        var morningOpen = new TimeSpan(9, 0, 0);
        var morningClose = new TimeSpan(11, 30, 0);
        var afternoonOpen = new TimeSpan(12, 30, 0);
        var afternoonClose = new TimeSpan(15, 0, 0);

        bool isMorningSession = time >= morningOpen && time <= morningClose;
        bool isAfternoonSession = time >= afternoonOpen && time <= afternoonClose;

        return isMorningSession || isAfternoonSession;
    }

    public CurrencyType GetCurrency() => CurrencyType.JPY;
}
