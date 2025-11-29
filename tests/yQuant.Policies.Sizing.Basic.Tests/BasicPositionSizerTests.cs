using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.Policies.Sizing.Basic;
using System.Collections.Generic;

namespace yQuant.Policies.Sizing.Basic.Tests;

[TestClass]
public class BasicPositionSizerTests
{
    private Mock<ILogger<BasicPositionSizer>>? _loggerMock;
    private IConfiguration? _configuration;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<BasicPositionSizer>>();

        var inMemorySettings = new Dictionary<string, string?> {
            {"MaxPositionRiskPct", "0.02"},
            {"MaxPortfolioAllocPct", "0.20"},
            {"StopLossPct", "0.05"},
            {"MinOrderAmt", "100000"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [TestMethod]
    public void CalculatePositionSize_WithSufficientFunds_CalculatesCorrectly()
    {
        // Arrange
        var sizer = new BasicPositionSizer(_loggerMock!.Object, _configuration!);
        var signal = new Signal
        {
            Ticker = "AAPL",
            Action = OrderAction.Buy,
            Price = 150m,
            Strength = 100,
            Currency = CurrencyType.USD,
            Exchange = "NASDAQ", // Added required property
            Source = "TestSource"   // Added required property
        };
        var account = new Account
        {
            Alias = "TestAccount",
            Number = "123",
            Broker = "Test",
            Active = true,
            Deposits = new Dictionary<CurrencyType, decimal> { { CurrencyType.USD, 1_000_000m } },
            Positions = new List<Position>()
        };

        // Act
        var order = sizer.CalculatePositionSize(signal, account);

        // Assert
        Assert.IsNotNull(order);
        // Calculation:
        // Equity = 1_000_000
        // RiskAmount = 1_000_000 * 0.02 = 20000
        // MaxAllocByRisk = 20000 / 0.05 = 400000
        // MaxAllocByPort = 1_000_000 * 0.20 = 200000
        // TargetAmount = min(400000, 200000) * (100/100) = 200000
        // ActualAmount = min(200000, 1_000_000) = 200000
        // Qty = floor(200000 / 150) = 1333
        Assert.AreEqual(1333, order.Qty);
        Assert.AreEqual(OrderType.Market, order.Type);
    }

    [TestMethod]
    public void CalculatePositionSize_WithInsufficientFunds_ReturnsNull()
    {
        // Arrange
        var sizer = new BasicPositionSizer(_loggerMock!.Object, _configuration!);
        var signal = new Signal
        {
            Ticker = "AAPL",
            Action = OrderAction.Buy,
            Price = 150m,
            Strength = 100,
            Currency = CurrencyType.USD,
            Exchange = "NASDAQ", // Added required property
            Source = "TestSource"   // Added required property
        };
        var account = new Account
        {
            Alias = "TestAccount",
            Number = "123",
            Broker = "Test",
            Active = true,
            Deposits = new Dictionary<CurrencyType, decimal> { { CurrencyType.USD, 100m } }, // Not enough cash
            Positions = new List<Position>()
        };

        // Act
        var order = sizer.CalculatePositionSize(signal, account);

        // Assert
        Assert.IsNull(order);
    }
}
