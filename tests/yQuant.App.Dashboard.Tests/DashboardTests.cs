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
using yQuant.Infra.Valkey.Services;


namespace yQuant.App.Dashboard.Tests;

[TestClass]
public class DashboardTests : Bunit.BunitContext
{
    private Mock<AssetService>? _assetServiceMock;
    private Mock<AccountCacheService>? _accountCacheServiceMock;
    private Mock<StockService>? _stockServiceMock;
    private Mock<OrderPublisher>? _orderPublisherMock;

    [TestInitialize]
    public void TestInitialize()
    {
        var loggerMock = new Mock<ILogger<AssetService>>();
        var redisMultiplexerMock = new Mock<IConnectionMultiplexer>();
        var redisServiceMock = new Mock<yQuant.Infra.Valkey.Interfaces.IValkeyService>();
        var storageValkeyMock = new Mock<yQuant.Infra.Valkey.Interfaces.IStorageValkeyService>();
        // Setup Valkey mocks
        var mockDb = new Mock<IDatabase>();
        var mockBatch = new Mock<IBatch>();
        mockDb.Setup(db => db.CreateBatch(It.IsAny<object>())).Returns(mockBatch.Object);
        mockBatch.Setup(b => b.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        redisMultiplexerMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        redisServiceMock.Setup(r => r.Connection).Returns(redisMultiplexerMock.Object);

        // Setup config for AssetService constructor
        var inMemorySettings = new Dictionary<string, string> {
            {"Web:Cache:AssetCacheDurationMinutes", "1"}
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _assetServiceMock = new Mock<AssetService>(redisMultiplexerMock.Object, redisServiceMock.Object, config, loggerMock.Object);

        // Setup AccountCacheService mock with correct constructor parameters
        var accountCacheLoggerMock = new Mock<ILogger<AccountCacheService>>();
        _accountCacheServiceMock = new Mock<AccountCacheService>(_assetServiceMock.Object, accountCacheLoggerMock.Object);

        // Setup StockService mock
        var stockServiceLoggerMock = new Mock<ILogger<StockService>>();
        var catalogRepoLoggerMock = new Mock<ILogger<StockCatalogRepository>>();
        var catalogRepo = new StockCatalogRepository(storageValkeyMock.Object, catalogRepoLoggerMock.Object);
        _stockServiceMock = new Mock<StockService>(stockServiceLoggerMock.Object, redisServiceMock.Object, catalogRepo);

        // Setup OrderPublisher mock
        var orderPublisherLoggerMock = new Mock<ILogger<OrderPublisher>>();
        _orderPublisherMock = new Mock<OrderPublisher>(orderPublisherLoggerMock.Object, redisMultiplexerMock.Object);

        Services.AddSingleton(_assetServiceMock.Object);
        Services.AddSingleton(_accountCacheServiceMock.Object);
        Services.AddSingleton(_stockServiceMock.Object);
        Services.AddSingleton(_orderPublisherMock.Object);
        Services.AddSingleton<IConfiguration>(config);
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

        _accountCacheServiceMock!.Setup(s => s.GetAccountsAsync(It.IsAny<bool>())).ReturnsAsync(accounts);
        _assetServiceMock!.Setup(s => s.GetAvailableAccountsAsync()).ReturnsAsync(new List<string> { "Test1", "Test2" });
        _assetServiceMock!.Setup(s => s.GetAccountOverviewAsync("Test1")).ReturnsAsync(accounts[0]);
        _assetServiceMock!.Setup(s => s.GetAccountOverviewAsync("Test2")).ReturnsAsync(accounts[1]);

        // Act
        var cut = Render<yQuant.App.Dashboard.Components.Pages.Summary>();

        // Assert
        cut.WaitForState(() => cut.FindAll("div.mud-card").Count == 2);
        Assert.HasCount(2, cut.FindAll("div.mud-card"));
        Assert.Contains("Test1", cut.Markup);
        Assert.Contains("Test2", cut.Markup);
    }

    [TestMethod]
    public void Dashboard_RendersLoadingState_WhenNoData()
    {
        // Arrange
        // Simulate slow loading or empty data initially
        _accountCacheServiceMock!.Setup(s => s.GetAccountsAsync(It.IsAny<bool>())).ReturnsAsync(new List<Account>());
        _assetServiceMock!.Setup(s => s.GetAvailableAccountsAsync()).ReturnsAsync(new List<string>());

        // Act
        var cut = Render<yQuant.App.Dashboard.Components.Pages.Summary>();

        // Assert
        Assert.Contains("Loading data...", cut.Markup);
    }
}
