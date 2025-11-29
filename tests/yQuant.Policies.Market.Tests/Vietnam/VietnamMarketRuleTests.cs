using Microsoft.VisualStudio.TestTools.UnitTesting;
using yQuant.Core.Models;
using yQuant.Policies.Market.Vietnam;

namespace yQuant.Policies.Market.Vietnam.Tests;

[TestClass]
public class VietnamMarketRuleTests
{
    private readonly VietnamMarketRule _rule = new VietnamMarketRule();

    [TestMethod]
    [DataRow("HOSE", true)]
    [DataRow("HNX", true)]
    [DataRow("UPCOM", false)]
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
        // 10:00 AM ICT (UTC+7) -> 03:00 AM UTC
        var marketOpenTime = new DateTime(2025, 11, 25, 3, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringAfternoonSession_ReturnsTrue()
    {
        // Arrange
        // 02:00 PM ICT (UTC+7) -> 07:00 AM UTC
        var marketOpenTime = new DateTime(2025, 11, 25, 7, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringLunchBreak_ReturnsFalse()
    {
        // Arrange
        // 12:00 PM ICT (UTC+7) -> 05:00 AM UTC
        var lunchTime = new DateTime(2025, 11, 25, 5, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(lunchTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OutsideTradingHours_ReturnsFalse()
    {
        // Arrange
        // 04:00 PM ICT (UTC+7) -> 09:00 AM UTC
        var marketClosedTime = new DateTime(2025, 11, 25, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(marketClosedTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OnWeekend_ReturnsFalse()
    {
        // Arrange
        // Saturday 10:00 AM ICT
        var weekend = new DateTime(2025, 11, 22, 3, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _rule.IsMarketOpen(weekend);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetCurrency_ReturnsVND()
    {
        // Act
        var currency = _rule.GetCurrency();

        // Assert
        Assert.AreEqual(CurrencyType.VND, currency);
    }
}
