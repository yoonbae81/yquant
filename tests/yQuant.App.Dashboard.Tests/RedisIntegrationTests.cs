using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.App.Dashboard.Services;
using yQuant.Infra.Valkey.Interfaces;

namespace yQuant.App.Dashboard.Tests;

[TestClass]
public class ValkeyIntegrationTests
{
    private Mock<IConnectionMultiplexer>? _mockValkey;
    private Mock<IDatabase>? _mockDb;
    private Mock<IValkeyService>? _mockValkeyService;
    private Mock<IConfiguration>? _mockConfig;
    private Mock<ILogger<AssetService>>? _mockLogger;
    private AssetService? _assetService;

    [TestInitialize]
    public void Setup()
    {
        _mockValkey = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockValkeyService = new Mock<IValkeyService>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AssetService>>();

        _mockValkey.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
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
            _mockValkey.Object,
            _mockValkeyService.Object,
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
        var redisValues = accounts.Select(a => (ValkeyValue)a).ToArray();

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
