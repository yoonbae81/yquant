using Microsoft.VisualStudio.TestTools.UnitTesting;
using yQuant.Core.Models;
using yQuant.Policies.Market.China;

namespace yQuant.Policies.Market.China.Tests;

[TestClass]
public class ChinaMarketRuleTests
{
    private readonly ChinaMarketRule _rule = new ChinaMarketRule();

    [TestMethod]
    [DataRow("SSE", true)]
    [DataRow("SZSE", true)]
    [DataRow("HKEX", false)]
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
        // 10:00 AM CST (UTC+8) -> 02:00 AM UTC
        var marketOpenTime = new DateTime(2025, 11, 25, 2, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringAfternoonSession_ReturnsTrue()
    {
        // Arrange
        // 02:00 PM CST (UTC+8) -> 06:00 AM UTC
        var marketOpenTime = new DateTime(2025, 11, 25, 6, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringLunchBreak_ReturnsFalse()
    {
        // Arrange
        // 12:00 PM CST (UTC+8) -> 04:00 AM UTC
        var lunchTime = new DateTime(2025, 11, 25, 4, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(lunchTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OutsideTradingHours_ReturnsFalse()
    {
        // Arrange
        // 04:00 PM CST (UTC+8) -> 08:00 AM UTC
        var marketClosedTime = new DateTime(2025, 11, 25, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketClosedTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OnWeekend_ReturnsFalse()
    {
        // Arrange
        // Saturday 10:00 AM CST
        var weekend = new DateTime(2025, 11, 22, 2, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(weekend);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetCurrency_ReturnsCNY()
    {
        // Act
        var currency = _rule.GetCurrency();

        // Assert
        Assert.AreEqual(CurrencyType.CNY, currency);
    }
}
