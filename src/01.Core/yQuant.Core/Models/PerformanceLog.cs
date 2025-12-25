using System;

namespace yQuant.Core.Models;

public class PerformanceLog
{
    public required DateOnly Date { get; set; }
    public required string AccountAlias { get; set; }
    public required CurrencyType Currency { get; set; }
    public required decimal TotalEquity { get; set; }
    public required decimal DailyPnL { get; set; }
    public required double DailyReturn { get; set; }
    public required int PositionsCount { get; set; }
}
