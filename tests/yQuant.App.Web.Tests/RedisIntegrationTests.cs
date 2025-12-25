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
        var inMemorySettings = new Dictionary<string, string> {
            {"Web:Cache:AssetCacheDurationMinutes", "1"}
        };
        _mockConfig = new Mock<IConfiguration>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _assetService = new AssetService(
            _mockRedis.Object,
            _mockRedisService.Object,
            config,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task GetAvailableAccountsAsync_ShouldDeserializeBrokerGatewayFormat()
    {
        // Arrange
        // This mimics the logic in BrokerGateway.Worker which uses account:index Set
        var accounts = new List<string> { "Trading", "Pension", "ISA", "IRP", "Yoonseo" };
        var redisValues = accounts.Select(a => (RedisValue)a).ToArray();

        _mockDb!.Setup(x => x.SetMembersAsync("account:index", It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisValues);

        // Act
        var result = await _assetService!.GetAvailableAccountsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(5, result);
        Assert.AreEqual("Trading", result[0]);
        Assert.AreEqual("Yoonseo", result[4]);
    }
}
