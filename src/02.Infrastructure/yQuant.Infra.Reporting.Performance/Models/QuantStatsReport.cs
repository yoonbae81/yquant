namespace yQuant.Infra.Reporting.Performance.Models;

public class QuantStatsReport
{
    public required string StrategyName { get; set; }
    public required DateOnly StartDate { get; set; }
    public required DateOnly EndDate { get; set; }
    public required double TotalReturn { get; set; }
    public required double CAGR { get; set; }
    public required double SharpeRatio { get; set; }
    public required double SortinoRatio { get; set; }
    public required double MaxDrawdown { get; set; }
    public required double Volatility { get; set; }
    public required double WinRate { get; set; }
    public required double ProfitFactor { get; set; }
    
    public string? HtmlReport { get; set; }
}
