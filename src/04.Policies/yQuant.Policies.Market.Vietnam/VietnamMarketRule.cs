using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Models;

namespace yQuant.Policies.Market.Vietnam;

public class VietnamMarketRule : IMarketRule
{
    private readonly TimeZoneInfo _vietnamZone;

    public VietnamMarketRule()
    {
        // Indochina Time (UTC+7)
        // Windows ID: "SE Asia Standard Time"
        try 
        {
            _vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            _vietnamZone = TimeZoneInfo.CreateCustomTimeZone("SE Asia Standard Time", new TimeSpan(7, 0, 0), "SE Asia Standard Time", "SE Asia Standard Time");
        }
    }

    public bool CanHandle(string exchange) =>
        new[] { "HOSE", "HNX" }.Contains(exchange.ToUpper());

    public bool IsMarketOpen(DateTime timestamp)
    {
        var ict = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _vietnamZone);

        // Check if it's a weekday (Monday to Friday)
        if (ict.DayOfWeek == DayOfWeek.Saturday || ict.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Trading hours:
        // Morning: 09:00 - 11:30
        // Afternoon: 13:00 - 15:00
        
        var time = ict.TimeOfDay;
        var morningOpen = new TimeSpan(9, 0, 0);
        var morningClose = new TimeSpan(11, 30, 0);
        var afternoonOpen = new TimeSpan(13, 0, 0);
        var afternoonClose = new TimeSpan(15, 0, 0);

        bool isMorningSession = time >= morningOpen && time <= morningClose;
        bool isAfternoonSession = time >= afternoonOpen && time <= afternoonClose;

        return isMorningSession || isAfternoonSession;
    }

    public CurrencyType GetCurrency() => CurrencyType.VND;
}
