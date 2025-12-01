using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.Policies.Market;
using System;
using System.Collections.Generic;

namespace yQuant.Policies.Market.Tests;

[TestClass]
public class USMarketRuleTests
{
    private Mock<ILogger<USMarketRule>>? _loggerMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<USMarketRule>>();
    }

    private USMarketRule CreateRule(bool allowPreMarket)
    {
        var inMemorySettings = new Dictionary<string, string?> {
            {"AllowPreMarket", allowPreMarket.ToString()}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        return new USMarketRule(_loggerMock!.Object, configuration);
    }

    [TestMethod]
    [DataRow("NYSE", true)]
    [DataRow("NASDAQ", true)]
    [DataRow("AMEX", true)]
    [DataRow("KRX", false)]
    public void CanHandle_ReturnsCorrectResult(string exchange, bool expected)
    {
        // Arrange
        var rule = CreateRule(false);

        // Act
        var result = rule.CanHandle(exchange);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringRegularHours_ReturnsTrue()
    {
        // Arrange
        var rule = CreateRule(false);
        // 10:00 AM ET on a Tuesday
        var marketOpenTime = new DateTime(2025, 11, 25, 15, 0, 0, DateTimeKind.Utc);

        // Act
        var result = rule.IsMarketOpen(marketOpenTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringPreMarket_WhenAllowed_ReturnsTrue()
    {
        // Arrange
        var rule = CreateRule(true);
        // 8:00 AM ET on a Tuesday
        var preMarketTime = new DateTime(2025, 11, 25, 13, 0, 0, DateTimeKind.Utc);

        // Act
        var result = rule.IsMarketOpen(preMarketTime);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMarketOpen_DuringPreMarket_WhenNotAllowed_ReturnsFalse()
    {
        // Arrange
        var rule = CreateRule(false);
        // 8:00 AM ET on a Tuesday
        var preMarketTime = new DateTime(2025, 11, 25, 13, 0, 0, DateTimeKind.Utc);

        // Act
        var result = rule.IsMarketOpen(preMarketTime);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetCurrency_ReturnsUSD()
    {
        // Arrange
        var rule = CreateRule(false);

        // Act
        var currency = rule.GetCurrency();

        // Assert
        Assert.AreEqual(CurrencyType.USD, currency);
    }
}
