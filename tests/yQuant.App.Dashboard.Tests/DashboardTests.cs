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
    private Mock<AssetService>? _assetServiceMock;

    [TestInitialize]
    public void TestInitialize()
    {
        var loggerMock = new Mock<ILogger<AssetService>>();
        var redisMultiplexerMock = new Mock<IConnectionMultiplexer>();
        var redisServiceMock = new Mock<yQuant.Infra.Redis.Interfaces.IRedisService>();
        var configMock = new Mock<IConfiguration>();

        // Setup config for AssetService constructor
        var configSectionMock = new Mock<IConfigurationSection>();
        configSectionMock.Setup(x => x.Value).Returns("1");
        configMock.Setup(c => c.GetSection("CacheSettings:AssetCacheDurationMinutes")).Returns(configSectionMock.Object);

        _assetServiceMock = new Mock<AssetService>(redisMultiplexerMock.Object, redisServiceMock.Object, configMock.Object, loggerMock.Object);

        Services.AddSingleton(_assetServiceMock.Object);
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

        _assetServiceMock!.Setup(s => s.GetAvailableAccountsAsync()).ReturnsAsync(new List<string> { "Test1", "Test2" });
        _assetServiceMock!.Setup(s => s.GetAccountOverviewAsync("Test1")).ReturnsAsync(accounts[0]);
        _assetServiceMock!.Setup(s => s.GetAccountOverviewAsync("Test2")).ReturnsAsync(accounts[1]);

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
        // Simulate slow loading or empty data initially
        _assetServiceMock!.Setup(s => s.GetAvailableAccountsAsync()).ReturnsAsync(new List<string>());

        // Act
        var cut = Render<yQuant.App.Dashboard.Components.Pages.Dashboard>();

        // Assert
        Assert.IsTrue(cut.Markup.Contains("Loading data..."));
    }
}
