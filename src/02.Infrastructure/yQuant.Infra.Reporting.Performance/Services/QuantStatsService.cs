using System.Text;
using yQuant.Core.Models;
using yQuant.Infra.Reporting.Performance.Interfaces;
using yQuant.Infra.Reporting.Performance.Models;

namespace yQuant.Infra.Reporting.Performance.Services;

public class QuantStatsService : IQuantStatsService
{
    private const int TradingDaysPerYear = 252;
    private const double RiskFreeRate = 0.0; // Assuming 0% for simplicity, could be configurable

    public QuantStatsReport GenerateReport(IEnumerable<PerformanceLog> logs, string strategyName = "Strategy")
    {
        var sortedLogs = logs.OrderBy(l => l.Date).ToList();
        if (!sortedLogs.Any())
        {
            return new QuantStatsReport
            {
                StrategyName = strategyName,
                StartDate = DateOnly.MinValue,
                EndDate = DateOnly.MinValue,
                TotalReturn = 0,
                CAGR = 0,
                SharpeRatio = 0,
                SortinoRatio = 0,
                MaxDrawdown = 0,
                Volatility = 0,
                WinRate = 0,
                ProfitFactor = 0,
                HtmlReport = "No data available."
            };
        }

        var startDate = sortedLogs.First().Date;
        var endDate = sortedLogs.Last().Date;
        var returns = sortedLogs.Select(l => l.DailyReturn).ToArray();
        
        // Metrics Calculation
        var totalReturn = CalculateTotalReturn(sortedLogs);
        var cagr = CalculateCAGR(totalReturn, startDate, endDate);
        var volatility = CalculateVolatility(returns);
        var sharpe = CalculateSharpeRatio(returns, volatility);
        var sortino = CalculateSortinoRatio(returns);
        var maxDrawdown = CalculateMaxDrawdown(sortedLogs);
        var winRate = CalculateWinRate(returns);
        var profitFactor = CalculateProfitFactor(sortedLogs);

        var report = new QuantStatsReport
        {
            StrategyName = strategyName,
            StartDate = startDate,
            EndDate = endDate,
            TotalReturn = totalReturn,
            CAGR = cagr,
            SharpeRatio = sharpe,
            SortinoRatio = sortino,
            MaxDrawdown = maxDrawdown,
            Volatility = volatility,
            WinRate = winRate,
            ProfitFactor = profitFactor
        };

        report.HtmlReport = GenerateHtmlReport(report);

        return report;
    }

    private double CalculateTotalReturn(List<PerformanceLog> logs)
    {
        if (logs.Count < 2) return 0;
        var startEquity = logs.First().TotalEquity;
        var endEquity = logs.Last().TotalEquity;
        if (startEquity == 0) return 0;
        return (double)((endEquity - startEquity) / startEquity);
    }

    private double CalculateCAGR(double totalReturn, DateOnly startDate, DateOnly endDate)
    {
        var days = startDate.DayNumber - endDate.DayNumber; // This will be negative
        var years = (endDate.DayNumber - startDate.DayNumber) / 365.25;
        if (years <= 0) return 0;
        return Math.Pow(1 + totalReturn, 1 / years) - 1;
    }

    private double CalculateVolatility(double[] returns)
    {
        if (returns.Length < 2) return 0;
        var avg = returns.Average();
        var sumSqDiff = returns.Sum(r => Math.Pow(r - avg, 2));
        var stdDev = Math.Sqrt(sumSqDiff / (returns.Length - 1));
        return stdDev * Math.Sqrt(TradingDaysPerYear);
    }

    private double CalculateSharpeRatio(double[] returns, double volatility)
    {
        if (volatility == 0) return 0;
        var annualizedReturn = returns.Average() * TradingDaysPerYear;
        return (annualizedReturn - RiskFreeRate) / volatility;
    }

    private double CalculateSortinoRatio(double[] returns)
    {
        var downsideReturns = returns.Where(r => r < 0).ToArray();
        if (!downsideReturns.Any()) return 0; // No downside risk? Infinite Sortino?

        var avg = returns.Average();
        var sumSqDiff = downsideReturns.Sum(r => Math.Pow(r, 2)); // Target return 0
        var downsideDev = Math.Sqrt(sumSqDiff / returns.Length) * Math.Sqrt(TradingDaysPerYear);

        if (downsideDev == 0) return 0;
        
        var annualizedReturn = avg * TradingDaysPerYear;
        return (annualizedReturn - RiskFreeRate) / downsideDev;
    }

    private double CalculateMaxDrawdown(List<PerformanceLog> logs)
    {
        double maxDrawdown = 0;
        decimal peak = logs[0].TotalEquity;

        foreach (var log in logs)
        {
            if (log.TotalEquity > peak)
            {
                peak = log.TotalEquity;
            }

            var drawdown = (double)((peak - log.TotalEquity) / peak);
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown; // Return as positive number (e.g. 0.15 for 15% drawdown)
    }

    private double CalculateWinRate(double[] returns)
    {
        if (returns.Length == 0) return 0;
        var wins = returns.Count(r => r > 0);
        return (double)wins / returns.Length;
    }

    private double CalculateProfitFactor(List<PerformanceLog> logs)
    {
        var grossProfit = logs.Where(l => l.DailyPnL > 0).Sum(l => l.DailyPnL);
        var grossLoss = Math.Abs(logs.Where(l => l.DailyPnL < 0).Sum(l => l.DailyPnL));

        if (grossLoss == 0) return grossProfit > 0 ? double.PositiveInfinity : 0;
        return (double)(grossProfit / grossLoss);
    }

    private string GenerateHtmlReport(QuantStatsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; max-width: 600px; }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background-color: #f2f2f2; }");
        sb.AppendLine(".positive { color: green; }");
        sb.AppendLine(".negative { color: red; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<h1>Strategy Report: {report.StrategyName}</h1>");
        sb.AppendLine($"<p>Period: {report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}</p>");
        
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
        sb.AppendLine($"<tr><td>Total Return</td><td class='{(report.TotalReturn >= 0 ? "positive" : "negative")}'>{report.TotalReturn:P2}</td></tr>");
        sb.AppendLine($"<tr><td>CAGR</td><td class='{(report.CAGR >= 0 ? "positive" : "negative")}'>{report.CAGR:P2}</td></tr>");
        sb.AppendLine($"<tr><td>Sharpe Ratio</td><td>{report.SharpeRatio:F2}</td></tr>");
        sb.AppendLine($"<tr><td>Sortino Ratio</td><td>{report.SortinoRatio:F2}</td></tr>");
        sb.AppendLine($"<tr><td>Max Drawdown</td><td class='negative'>-{report.MaxDrawdown:P2}</td></tr>");
        sb.AppendLine($"<tr><td>Volatility (Ann.)</td><td>{report.Volatility:P2}</td></tr>");
        sb.AppendLine($"<tr><td>Win Rate</td><td>{report.WinRate:P2}</td></tr>");
        sb.AppendLine($"<tr><td>Profit Factor</td><td>{report.ProfitFactor:F2}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
    public string GenerateCsvReport(IEnumerable<PerformanceLog> logs)
    {
        var sb = new StringBuilder();
        // QuantStats expects a CSV with Date and Value (or similar) columns.
        // Based on typical usage, it often takes a Series.
        // We will output: Date,TotalEquity,DailyReturn
        sb.AppendLine("Date,TotalEquity,DailyReturn");

        foreach (var log in logs.OrderBy(l => l.Date))
        {
            sb.AppendLine($"{log.Date:yyyy-MM-dd},{log.TotalEquity},{log.DailyReturn}");
        }

        return sb.ToString();
    }
}
