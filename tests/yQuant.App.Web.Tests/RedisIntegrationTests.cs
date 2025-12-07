using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.App.Web.Services;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.App.Web.Tests;

[TestClass]
public class RedisIntegrationTests
{
    private Mock<IConnectionMultiplexer>? _mockRedis;
    private Mock<IDatabase>? _mockDb;
    private Mock<IRedisService>? _mockRedisService;
    private Mock<IConfiguration>? _mockConfig;
    private Mock<ILogger<AssetService>>? _mockLogger;
    private AssetService? _assetService;

    [TestInitialize]
    public void Setup()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockRedisService = new Mock<IRedisService>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AssetService>>();

        _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);

        // Fix: Setup configuration to return a default value for the cache duration
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("1");
        _mockConfig.Setup(c => c.GetSection("CacheSettings:AssetCacheDurationMinutes")).Returns(mockSection.Object);

        _assetService = new AssetService(
            _mockRedis.Object,
            _mockRedisService.Object,
            _mockConfig.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task GetAvailableAccountsAsync_ShouldDeserializeBrokerGatewayFormat()
    {
        // Arrange
        // This mimics the logic in BrokerGateway.Worker.RegisterAccountsAsync
        var accounts = new List<string> { "Trading", "Pension", "ISA", "IRP", "Yoonseo" };
        var json = JsonSerializer.Serialize(accounts);

        _mockDb!.Setup(x => x.StringGetAsync("broker:accounts", It.IsAny<CommandFlags>()))
            .ReturnsAsync(json);

        // Act
        var result = await _assetService!.GetAvailableAccountsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.Count);
        Assert.AreEqual("Trading", result[0]);
        Assert.AreEqual("Yoonseo", result[4]);
    }
}
