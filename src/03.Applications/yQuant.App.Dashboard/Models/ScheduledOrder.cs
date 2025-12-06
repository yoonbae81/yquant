using yQuant.Core.Models;

namespace yQuant.App.Dashboard.Models;

public enum ScheduleTimeMode
{
    FixedTime,
    AfterMarketOpen
}

public class ScheduledOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AccountAlias { get; set; }
    public required string Ticker { get; set; }
    public ExchangeCode Exchange { get; set; } = ExchangeCode.NASDAQ;
    public CurrencyType Currency { get; set; } = CurrencyType.USD;
    public required OrderAction Action { get; set; }
    public int? Quantity { get; set; }

    // Scheduling Logic
    // If user wants 'Daily', the UI should populate this with all 7 days (or Mon-Fri)
    public HashSet<DayOfWeek> DaysOfWeek { get; set; } = new();
    public ScheduleTimeMode TimeMode { get; set; }
    public TimeSpan TimeConfig { get; set; } // Fixed time (e.g. 15:00) or Offset (e.g. 01:00)

    // State
    public DateTime? NextExecutionTime { get; set; }
    public DateTime? LastExecutedTime { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
