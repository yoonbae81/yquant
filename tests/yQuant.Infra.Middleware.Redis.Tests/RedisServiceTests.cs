using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using yQuant.Infra.Middleware.Redis.Services;
using System.Text.Json;

namespace yQuant.Infra.Middleware.Redis.Tests;

public class RedisServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockConnection;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisService>> _mockLogger;
    private readonly RedisService _service;

    public RedisServiceTests()
    {
        _mockConnection = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisService>>();

        _mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);

        _service = new RedisService(_mockConnection.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAsync_ReturnsDeserializedObject_WhenKeyExists()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = new TestObject { Id = 1, Name = "Test" };
        var json = JsonSerializer.Serialize(expectedValue);
        
        _mockDatabase.Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
                     .ReturnsAsync(json);

        // Act
        var result = await _service.GetAsync<TestObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "missing-key";
        _mockDatabase.Setup(d => d.StringGetAsync(key, It.IsAny<CommandFlags>()))
                     .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetAsync<TestObject>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_SetsValueInRedis()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };

        // Act
        await _service.SetAsync(key, value);

        // Assert
        _mockDatabase.Verify(d => d.StringSetAsync(key, It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
