using Microsoft.VisualStudio.TestTools.UnitTesting;
using yQuant.Core.Models;
using yQuant.Policies.Market.Japan;

namespace yQuant.Policies.Market.Japan.Tests;

[TestClass]
public class JapanMarketRuleTests
{
    private readonly JapanMarketRule _rule = new JapanMarketRule();

    [TestMethod]
    [DataRow("TSE", true)]
    [DataRow("OSE", false)]
    [DataRow("NYSE", false)]
    public void CanHandle_ReturnsCorrectResult(string exchange, bool expected)
    {
        // Act
        var result = _rule.CanHandle(exchange);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringMorningSession_ReturnsTrue()
    {
        // Arrange
        // 10:00 AM JST (UTC+9) -> 01:00 AM UTC
        var marketOpenTime = new DateTime(2025, 11, 25, 1, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringAfternoonSession_ReturnsTrue()
    {
        // Arrange
        // 01:00 PM JST (UTC+9) -> 04:00 AM UTC
        var marketOpenTime = new DateTime(2025, 11, 25, 4, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringLunchBreak_ReturnsFalse()
    {
        // Arrange
        // 12:00 PM JST (UTC+9) -> 03:00 AM UTC
        var lunchTime = new DateTime(2025, 11, 25, 3, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(lunchTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OutsideTradingHours_ReturnsFalse()
    {
        // Arrange
        // 04:00 PM JST (UTC+9) -> 07:00 AM UTC
        var marketClosedTime = new DateTime(2025, 11, 25, 7, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketClosedTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OnWeekend_ReturnsFalse()
    {
        // Arrange
        // Saturday 10:00 AM JST
        var weekend = new DateTime(2025, 11, 22, 1, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(weekend);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetCurrency_ReturnsJPY()
    {
        // Act
        var currency = _rule.GetCurrency();

        // Assert
        Assert.AreEqual(CurrencyType.JPY, currency);
    }
}
