using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Models;

namespace yQuant.Policies.Market.China;

public class ChinaMarketRule : IMarketRule
{
    private readonly TimeZoneInfo _chinaZone;

    public ChinaMarketRule()
    {
        // China Standard Time (UTC+8)
        // Windows ID: "China Standard Time"
        // IANA ID: "Asia/Shanghai"
        // Using Windows ID for compatibility as the user is on Windows
        try 
        {
            _chinaZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
             // Fallback or handle cross-platform if needed, but user is on Windows
            _chinaZone = TimeZoneInfo.CreateCustomTimeZone("China Standard Time", new TimeSpan(8, 0, 0), "China Standard Time", "China Standard Time");
        }
    }

    public bool CanHandle(string exchange) =>
        new[] { "SSE", "SZSE" }.Contains(exchange.ToUpper());

    public bool IsMarketOpen(DateTime timestamp)
    {
        var cst = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _chinaZone);

        // Check if it's a weekday (Monday to Friday)
        if (cst.DayOfWeek == DayOfWeek.Saturday || cst.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Trading hours:
        // Morning: 09:30 - 11:30
        // Afternoon: 13:00 - 15:00
        
        var time = cst.TimeOfDay;
        var morningOpen = new TimeSpan(9, 30, 0);
        var morningClose = new TimeSpan(11, 30, 0);
        var afternoonOpen = new TimeSpan(13, 0, 0);
        var afternoonClose = new TimeSpan(15, 0, 0);

        bool isMorningSession = time >= morningOpen && time <= morningClose;
        bool isAfternoonSession = time >= afternoonOpen && time <= afternoonClose;

        return isMorningSession || isAfternoonSession;
    }

    public CurrencyType GetCurrency() => CurrencyType.CNY;
}
