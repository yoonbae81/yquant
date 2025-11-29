using Xunit;
using yQuant.Core.Models;
using yQuant.Infra.Reporting.Performance.Services;

namespace yQuant.Infra.Reporting.Performance.Tests;

public class QuantStatsServiceTests
{
    private readonly QuantStatsService _service;

    public QuantStatsServiceTests()
    {
        _service = new QuantStatsService();
    }

    [Fact]
    public void GenerateReport_WithEmptyLogs_ReturnsEmptyReport()
    {
        // Arrange
        var logs = Enumerable.Empty<PerformanceLog>();

        // Act
        var report = _service.GenerateReport(logs);

        // Assert
        Assert.Equal(0, report.TotalReturn);
        Assert.Equal("No data available.", report.HtmlReport);
    }

    [Fact]
    public void GenerateReport_WithValidLogs_CalculatesMetricsCorrectly()
    {
        // Arrange
        var startDate = new DateOnly(2023, 1, 1);
        var logs = new List<PerformanceLog>
        {
            new() { Date = startDate, AccountAlias = "1", Currency = CurrencyType.USD, TotalEquity = 10000, DailyPnL = 0, DailyReturn = 0, PositionsCount = 0 },
            new() { Date = startDate.AddDays(1), AccountAlias = "1", Currency = CurrencyType.USD, TotalEquity = 10500, DailyPnL = 500, DailyReturn = 0.05, PositionsCount = 1 },
            new() { Date = startDate.AddDays(2), AccountAlias = "1", Currency = CurrencyType.USD, TotalEquity = 10200, DailyPnL = -300, DailyReturn = -0.0285, PositionsCount = 1 },
            new() { Date = startDate.AddDays(3), AccountAlias = "1", Currency = CurrencyType.USD, TotalEquity = 11000, DailyPnL = 800, DailyReturn = 0.0784, PositionsCount = 1 }
        };

        // Act
        var report = _service.GenerateReport(logs);

        // Assert
        Assert.Equal(0.1, report.TotalReturn, 2); // (11000 - 10000) / 10000 = 0.1
        Assert.True(report.CAGR > 0);
        Assert.True(report.SharpeRatio > 0);
        Assert.True(report.MaxDrawdown > 0); // There was a drawdown from 10500 to 10200
        Assert.Contains("Strategy Report", report.HtmlReport);
    }
}
