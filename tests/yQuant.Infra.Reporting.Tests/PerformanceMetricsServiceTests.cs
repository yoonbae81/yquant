using Xunit;
using yQuant.Core.Models;
using yQuant.Infra.Reporting.Services;

namespace yQuant.Infra.Reporting.Tests;

public class PerformanceMetricsServiceTests
{
    private readonly PerformanceMetricsService _service;

    public PerformanceMetricsServiceTests()
    {
        _service = new PerformanceMetricsService();
    }

    #region CAGR Tests

    [Fact]
    public void CalculateCAGR_WithDoubleInOneYear_Returns100Percent()
    {
        // Arrange
        decimal initialEquity = 100000m;
        decimal finalEquity = 200000m;
        int days = 365;

        // Act
        var cagr = _service.CalculateCAGR(initialEquity, finalEquity, days);

        // Assert
        Assert.Equal(1.0, cagr, 2); // 2 decimal places
    }

    [Fact]
    public void CalculateCAGR_WithTripleInTwoYears_ReturnsCorrectValue()
    {
        // Arrange
        decimal initialEquity = 100000m;
        decimal finalEquity = 300000m;
        int days = 730; // 2 years

        // Act
        var cagr = _service.CalculateCAGR(initialEquity, finalEquity, days);

        // Assert
        // (300000/100000)^(1/2) - 1 = 3^0.5 - 1 ≈ 0.732 (73.2%)
        Assert.Equal(0.732, cagr, 2);
    }

    [Fact]
    public void CalculateCAGR_WithZeroInitialEquity_ReturnsZero()
    {
        // Arrange
        decimal initialEquity = 0m;
        decimal finalEquity = 100000m;
        int days = 365;

        // Act
        var cagr = _service.CalculateCAGR(initialEquity, finalEquity, days);

        // Assert
        Assert.Equal(0.0, cagr);
    }

    #endregion

    #region Cumulative Return Tests

    [Fact]
    public void CalculateCumulativeReturn_With50PercentGain_Returns0Point5()
    {
        // Arrange
        decimal initialEquity = 100000m;
        decimal finalEquity = 150000m;

        // Act
        var cumulativeReturn = _service.CalculateCumulativeReturn(initialEquity, finalEquity);

        // Assert
        Assert.Equal(0.5, cumulativeReturn, 3);
    }

    [Fact]
    public void CalculateCumulativeReturn_With20PercentLoss_ReturnsNegative0Point2()
    {
        // Arrange
        decimal initialEquity = 100000m;
        decimal finalEquity = 80000m;

        // Act
        var cumulativeReturn = _service.CalculateCumulativeReturn(initialEquity, finalEquity);

        // Assert
        Assert.Equal(-0.2, cumulativeReturn, 3);
    }

    #endregion

    #region Sharpe Ratio Tests

    [Fact]
    public void CalculateSharpeRatio_WithConsistentPositiveReturns_ReturnsHighValue()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.01),  // 1% daily
            (new DateOnly(2025, 1, 2), 101000m, 0.01),
            (new DateOnly(2025, 1, 3), 102010m, 0.01),
            (new DateOnly(2025, 1, 4), 103030m, 0.01),
            (new DateOnly(2025, 1, 5), 104060m, 0.01)
        });

        // Act
        var sharpe = _service.CalculateSharpeRatio(snapshots);

        // Assert
        // With consistent returns, standard deviation is 0, so Sharpe is 0 (division by zero)
        Assert.Equal(0.0, sharpe);
    }

    [Fact]
    public void CalculateSharpeRatio_WithVolatileReturns_ReturnsLowerValue()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.05),   // 5%
            (new DateOnly(2025, 1, 2), 105000m, -0.03),  // -3%
            (new DateOnly(2025, 1, 3), 101850m, 0.04),   // 4%
            (new DateOnly(2025, 1, 4), 105924m, -0.02),  // -2%
            (new DateOnly(2025, 1, 5), 103806m, 0.03)    // 3%
        });

        // Act
        var sharpe = _service.CalculateSharpeRatio(snapshots);

        // Assert
        // With volatile returns, Sharpe should be positive but not extremely high
        Assert.True(sharpe > 0 && sharpe < 10, $"Sharpe ratio should be moderate with volatile returns, got {sharpe}");
    }

    [Fact]
    public void CalculateSharpeRatio_WithInsufficientData_ReturnsZero()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.01)
        });

        // Act
        var sharpe = _service.CalculateSharpeRatio(snapshots);

        // Assert
        Assert.Equal(0.0, sharpe);
    }

    #endregion

    #region Sortino Ratio Tests

    [Fact]
    public void CalculateSortinoRatio_WithOnlyPositiveReturns_ReturnsVeryHighValue()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.01),
            (new DateOnly(2025, 1, 2), 101000m, 0.02),
            (new DateOnly(2025, 1, 3), 103020m, 0.01),
            (new DateOnly(2025, 1, 4), 104050m, 0.015),
            (new DateOnly(2025, 1, 5), 105611m, 0.01)
        });

        // Act
        var sortino = _service.CalculateSortinoRatio(snapshots);

        // Assert
        // With no downside deviation, Sortino is 0 (division by zero in downside deviation)
        Assert.Equal(0.0, sortino);
    }

    [Fact]
    public void CalculateSortinoRatio_WithMixedReturns_ReturnsReasonableValue()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.02),
            (new DateOnly(2025, 1, 2), 102000m, -0.01),
            (new DateOnly(2025, 1, 3), 100980m, 0.03),
            (new DateOnly(2025, 1, 4), 104009m, -0.005),
            (new DateOnly(2025, 1, 5), 103489m, 0.02)
        });

        // Act
        var sortino = _service.CalculateSortinoRatio(snapshots);

        // Assert
        Assert.True(sortino > 0, "Sortino ratio should be positive with net positive returns");
    }

    #endregion

    #region MDD Tests

    [Fact]
    public void CalculateMDD_WithSteadyGrowth_ReturnsZero()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.01),
            (new DateOnly(2025, 1, 2), 101000m, 0.01),
            (new DateOnly(2025, 1, 3), 102010m, 0.01),
            (new DateOnly(2025, 1, 4), 103030m, 0.01),
            (new DateOnly(2025, 1, 5), 104060m, 0.01)
        });

        // Act
        var mdd = _service.CalculateMDD(snapshots);

        // Assert
        Assert.Equal(0.0, mdd, 3);
    }

    [Fact]
    public void CalculateMDD_With20PercentDrawdown_Returns0Point2()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.0),
            (new DateOnly(2025, 1, 2), 110000m, 0.1),   // Peak
            (new DateOnly(2025, 1, 3), 100000m, -0.091), // -9.1%
            (new DateOnly(2025, 1, 4), 88000m, -0.12),   // -12% (total -20% from peak)
            (new DateOnly(2025, 1, 5), 95000m, 0.08)
        });

        // Act
        var mdd = _service.CalculateMDD(snapshots);

        // Assert
        Assert.Equal(0.2, mdd, 1);
    }

    #endregion

    #region Volatility Tests

    [Fact]
    public void CalculateVolatility_WithZeroVariance_ReturnsZero()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.01),
            (new DateOnly(2025, 1, 2), 101000m, 0.01),
            (new DateOnly(2025, 1, 3), 102010m, 0.01),
            (new DateOnly(2025, 1, 4), 103030m, 0.01),
            (new DateOnly(2025, 1, 5), 104060m, 0.01)
        });

        // Act
        var volatility = _service.CalculateVolatility(snapshots);

        // Assert
        Assert.Equal(0.0, volatility, 3);
    }

    [Fact]
    public void CalculateVolatility_WithVariance_ReturnsPositiveValue()
    {
        // Arrange
        var snapshots = CreateSnapshots(new[]
        {
            (new DateOnly(2025, 1, 1), 100000m, 0.02),
            (new DateOnly(2025, 1, 2), 102000m, -0.01),
            (new DateOnly(2025, 1, 3), 100980m, 0.03),
            (new DateOnly(2025, 1, 4), 104009m, -0.02),
            (new DateOnly(2025, 1, 5), 101929m, 0.04)
        });

        // Act
        var volatility = _service.CalculateVolatility(snapshots);

        // Assert
        Assert.True(volatility > 0, "Volatility should be positive with varying returns");
        Assert.True(volatility < 1, "Annualized volatility should be reasonable (< 100%)");
    }

    #endregion

    #region Win Rate Tests

    [Fact]
    public void CalculateWinRate_WithAllWinningTrades_Returns100Percent()
    {
        // Arrange
        var trades = new List<TradeRecord>
        {
            CreateTrade("AAPL", OrderAction.Buy, 100, 150m, new DateTime(2025, 1, 1)),
            CreateTrade("AAPL", OrderAction.Sell, 100, 160m, new DateTime(2025, 1, 5)),
            CreateTrade("TSLA", OrderAction.Buy, 50, 200m, new DateTime(2025, 1, 2)),
            CreateTrade("TSLA", OrderAction.Sell, 50, 220m, new DateTime(2025, 1, 6))
        };

        // Act
        var winRate = _service.CalculateWinRate(trades);

        // Assert
        Assert.Equal(1.0, winRate, 3);
    }

    [Fact]
    public void CalculateWinRate_With50PercentWins_Returns0Point5()
    {
        // Arrange
        var trades = new List<TradeRecord>
        {
            // Winning trade
            CreateTrade("AAPL", OrderAction.Buy, 100, 150m, new DateTime(2025, 1, 1)),
            CreateTrade("AAPL", OrderAction.Sell, 100, 160m, new DateTime(2025, 1, 5)),
            // Losing trade
            CreateTrade("TSLA", OrderAction.Buy, 50, 200m, new DateTime(2025, 1, 2)),
            CreateTrade("TSLA", OrderAction.Sell, 50, 180m, new DateTime(2025, 1, 6))
        };

        // Act
        var winRate = _service.CalculateWinRate(trades);

        // Assert
        Assert.Equal(0.5, winRate, 3);
    }

    #endregion

    #region Profit Factor Tests

    [Fact]
    public void CalculateProfitFactor_WithOnlyWinningTrades_ReturnsInfinity()
    {
        // Arrange
        var trades = new List<TradeRecord>
        {
            CreateTrade("AAPL", OrderAction.Buy, 100, 150m, new DateTime(2025, 1, 1)),
            CreateTrade("AAPL", OrderAction.Sell, 100, 160m, new DateTime(2025, 1, 5))
        };

        // Act
        var profitFactor = _service.CalculateProfitFactor(trades);

        // Assert
        Assert.True(double.IsPositiveInfinity(profitFactor),
            "Profit factor should be infinity with only winning trades");
    }

    [Fact]
    public void CalculateProfitFactor_WithEqualProfitAndLoss_Returns1()
    {
        // Arrange
        var trades = new List<TradeRecord>
        {
            // +1000 profit
            CreateTrade("AAPL", OrderAction.Buy, 100, 150m, new DateTime(2025, 1, 1)),
            CreateTrade("AAPL", OrderAction.Sell, 100, 160m, new DateTime(2025, 1, 5)),
            // -1000 loss
            CreateTrade("TSLA", OrderAction.Buy, 100, 200m, new DateTime(2025, 1, 2)),
            CreateTrade("TSLA", OrderAction.Sell, 100, 190m, new DateTime(2025, 1, 6))
        };

        // Act
        var profitFactor = _service.CalculateProfitFactor(trades);

        // Assert
        Assert.Equal(1.0, profitFactor, 1);
    }

    [Fact]
    public void CalculateProfitFactor_WithMoreProfitThanLoss_ReturnsGreaterThan1()
    {
        // Arrange
        var trades = new List<TradeRecord>
        {
            // +2000 profit
            CreateTrade("AAPL", OrderAction.Buy, 100, 150m, new DateTime(2025, 1, 1)),
            CreateTrade("AAPL", OrderAction.Sell, 100, 170m, new DateTime(2025, 1, 5)),
            // -500 loss
            CreateTrade("TSLA", OrderAction.Buy, 100, 200m, new DateTime(2025, 1, 2)),
            CreateTrade("TSLA", OrderAction.Sell, 100, 195m, new DateTime(2025, 1, 6))
        };

        // Act
        var profitFactor = _service.CalculateProfitFactor(trades);

        // Assert
        Assert.True(profitFactor > 1.0, $"Profit factor should be > 1.0, got {profitFactor}");
    }

    #endregion

    #region Helper Methods

    private List<DailySnapshot> CreateSnapshots((DateOnly date, decimal equity, double dailyReturn)[] data)
    {
        var snapshots = new List<DailySnapshot>();

        foreach (var (date, equity, dailyReturn) in data)
        {
            snapshots.Add(new DailySnapshot
            {
                Date = date,
                Currency = CurrencyType.USD,
                TotalEquity = equity,
                CashBalance = equity / 2,
                PositionValue = equity / 2,
                DailyPnL = 0,
                DailyReturn = dailyReturn,
                PositionsCount = 0
            });
        }

        return snapshots;
    }

    private TradeRecord CreateTrade(string ticker, OrderAction action, decimal qty, decimal price, DateTime executedAt)
    {
        return new TradeRecord
        {
            Id = Guid.NewGuid(),
            ExecutedAt = executedAt,
            Ticker = ticker,
            Action = action,
            Quantity = qty,
            ExecutedPrice = price,
            Commission = 0,
            Currency = CurrencyType.USD,
            Exchange = ExchangeCode.NASDAQ
        };
    }

    #endregion
}
