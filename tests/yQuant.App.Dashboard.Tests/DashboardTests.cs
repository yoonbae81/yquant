using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bunit;
using Moq;
using yQuant.App.Dashboard.Services;
using yQuant.Core.Models;
using System.Collections.Generic;
using yQuant.App.Dashboard.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace yQuant.App.Dashboard.Tests;

[TestClass]
public class DashboardTests : Bunit.BunitContext
{
    private Mock<RedisService>? _redisServiceMock;

    [TestInitialize]
    public void TestInitialize()
    {
        var loggerMock = new Mock<ILogger<RedisService>>();
        var redisMultiplexerMock = new Mock<IConnectionMultiplexer>();
        var configMock = new Mock<IConfiguration>();

        var configSectionMock = new Mock<IConfigurationSection>();
        configSectionMock.Setup(x => x.Value).Returns("1");
        configMock.Setup(c => c.GetSection("RedisSyncIntervalSeconds")).Returns(configSectionMock.Object);
        
        _redisServiceMock = new Mock<RedisService>(loggerMock.Object, redisMultiplexerMock.Object, configMock.Object);
        
        Services.AddSingleton(_redisServiceMock.Object);
        Services.AddMudServices(); // Add MudBlazor services
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestMethod]
    public void Dashboard_RendersCorrectly_WithData()
    {
        // Arrange
        var accounts = new List<Account>
        {
            new Account { Alias = "Test1", Broker = "Broker1", Number = "123", AppKey = "test_key", AppSecret = "test_secret", Active = true, Deposits = new Dictionary<CurrencyType, decimal>{{CurrencyType.USD, 1000}} },
            new Account { Alias = "Test2", Broker = "Broker2", Number = "456", AppKey = "test_key", AppSecret = "test_secret", Active = true, Deposits = new Dictionary<CurrencyType, decimal>{{CurrencyType.KRW, 2000000}} }
        };
        _redisServiceMock.Setup(s => s.GetAccounts()).Returns(accounts);

        // Act
        var cut = Render<yQuant.App.Dashboard.Components.Pages.Dashboard>();

        // Assert
        cut.WaitForState(() => cut.FindAll("div.mud-card").Count == 2);
        Assert.AreEqual(2, cut.FindAll("div.mud-card").Count);
        Assert.IsTrue(cut.Markup.Contains("Test1"));
        Assert.IsTrue(cut.Markup.Contains("Test2"));
    }
    
    [TestMethod]
    public void Dashboard_RendersLoadingState_WhenNoData()
    {
        // Arrange
        _redisServiceMock.Setup(s => s.GetAccounts()).Returns(new List<Account>());

        // Act
        var cut = Render<yQuant.App.Dashboard.Components.Pages.Dashboard>();

        // Assert
        Assert.IsTrue(cut.Markup.Contains("Loading data..."));
    }
}
