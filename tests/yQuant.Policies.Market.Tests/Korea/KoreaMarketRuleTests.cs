using Microsoft.VisualStudio.TestTools.UnitTesting;
using yQuant.Core.Models;
using yQuant.Policies.Market.Korea;
using System;

namespace yQuant.Policies.Market.Korea.Tests;

[TestClass]
public class KoreaMarketRuleTests
{
    private readonly KoreaMarketRule _rule = new KoreaMarketRule();

    [TestMethod]
    [DataRow("KRX", true)]
    [DataRow("KOSPI", true)]
    [DataRow("KOSDAQ", true)]
    [DataRow("NASDAQ", false)]
    [DataRow("NYSE", false)]
    public void CanHandle_ReturnsCorrectResult(string exchange, bool expected)
    {
        // Act
        var result = _rule.CanHandle(exchange);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringTradingHours_ReturnsTrue()
    {
        // Arrange
        // Assuming today is a weekday. This test might be fragile if run on a weekend.
        // A more robust test would mock the current time.
        // Let's create a specific UTC time that corresponds to a weekday in KST.
        // e.g., A Tuesday in UTC which is also Tuesday in KST
        var marketOpenTime = new DateTime(2025, 11, 25, 2, 0, 0, DateTimeKind.Utc); // 11 AM KST

        // Act
        var result = _rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_OutsideTradingHours_ReturnsFalse()
    {
        // Arrange
        var marketClosedTime = new DateTime(2025, 11, 25, 8, 0, 0, DateTimeKind.Utc); // 5 PM KST

        // Act
        var result = _rule.IsMarketOpen(marketClosedTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMarketOpen_OnWeekend_ReturnsFalse()
    {
        // Arrange
        var weekend = new DateTime(2025, 11, 22, 2, 0, 0, DateTimeKind.Utc); // A Saturday

        // Act
        var result = _rule.IsMarketOpen(weekend);

        // Assert
        Assert.IsFalse(result);
    }


    [TestMethod]
    public void GetCurrency_ReturnsKRW()
    {
        // Act
        var currency = _rule.GetCurrency();

        // Assert
        Assert.AreEqual(CurrencyType.KRW, currency);
    }
}