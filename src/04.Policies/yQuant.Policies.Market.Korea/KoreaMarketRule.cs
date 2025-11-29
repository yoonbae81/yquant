using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Models;
using System.Linq;

namespace yQuant.Policies.Market.Korea;

public class KoreaMarketRule : IMarketRule
{
    private readonly TimeZoneInfo _koreaZone;

    public KoreaMarketRule()
    {
        _koreaZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
    }

    public bool CanHandle(string exchange) =>
        new[] { "KRX", "KOSPI", "KOSDAQ" }.Contains(exchange.ToUpper());

    public bool IsMarketOpen(DateTime timestamp)
    {
        var kst = TimeZoneInfo.ConvertTimeFromUtc(timestamp, _koreaZone);

        // Check if it's a weekday (Monday to Friday)
        if (kst.DayOfWeek == DayOfWeek.Saturday || kst.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // Check trading hours (09:00 to 15:30 KST)
        var marketOpenTime = new TimeSpan(9, 0, 0);
        var marketCloseTime = new TimeSpan(15, 30, 0);

        return kst.TimeOfDay >= marketOpenTime && kst.TimeOfDay <= marketCloseTime;
        // Note: Real-world implementation would also check for holidays.
    }

    public CurrencyType GetCurrency() => CurrencyType.KRW;
}
