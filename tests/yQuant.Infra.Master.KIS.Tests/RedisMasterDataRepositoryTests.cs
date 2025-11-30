using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Infra.Master.KIS;
using yQuant.Infra.Middleware.Redis.Interfaces;

namespace yQuant.Infra.Master.KIS.Tests;

[TestClass]
public class RedisMasterDataRepositoryTests
{
    private Mock<IConnectionMultiplexer> _mockRedis = null!;
    private Mock<IDatabase> _mockDatabase = null!;
    private Mock<IBatch> _mockBatch = null!;
    private Mock<IRedisService> _mockRedisService = null!;
    private Mock<ILogger<RedisMasterDataRepository>> _mockLogger = null!;
    private RedisMasterDataRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockBatch = new Mock<IBatch>();
        _mockRedisService = new Mock<IRedisService>();
        _mockLogger = new Mock<ILogger<RedisMasterDataRepository>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
        _mockDatabase.Setup(db => db.CreateBatch())
            .Returns(_mockBatch.Object);
        
        _mockRedisService.Setup(s => s.Connection).Returns(_mockRedis.Object);

        _repository = new RedisMasterDataRepository(_mockRedisService.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task SaveBatchAsync_ShouldSaveAllStocksWithExpiration()
    {
        // Arrange
        var stocks = new List<StockMaster>
        {
            new StockMaster 
            { 
                Ticker = "005930", 
                Name = "?¼ì„±?„ìž", 
                Exchange = "KOSPI", 
                Currency = CurrencyType.KRW 
            },
            new StockMaster 
            { 
                Ticker = "AAPL", 
                Name = "Apple Inc.", 
                Exchange = "NASDAQ", 
                Currency = CurrencyType.USD 
            }
        };

        _mockBatch.Setup(b => b.HashSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<HashEntry[]>(),
            It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        _mockBatch.Setup(b => b.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()))
            .Returns(Task.FromResult(true));

        // Act
        await _repository.SaveBatchAsync(stocks);

        // Assert
        // _mockBatch.Verify(b => b.Execute(), Times.Once);
        _mockBatch.Verify(b => b.HashSetAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("cache:master:")),
            It.Is<HashEntry[]>(entries => 
                entries.Any(e => e.Name == "name") &&
                entries.Any(e => e.Name == "exchange") &&
                entries.Any(e => e.Name == "currency")),
            It.IsAny<CommandFlags>()), Times.Exactly(2));
        _mockBatch.Verify(b => b.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalHours == 25),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task SaveBatchAsync_WithEmptyList_ShouldNotCallRedis()
    {
        // Arrange
        var stocks = new List<StockMaster>();

        // Act
        await _repository.SaveBatchAsync(stocks);

        // Assert
        // _mockBatch.Verify(b => b.Execute(), Times.Once);
        _mockBatch.Verify(b => b.HashSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<HashEntry[]>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task GetByTickerAsync_WithExistingTicker_ShouldReturnStock()
    {
        // Arrange
        var ticker = "005930";
        var hashEntries = new HashEntry[]
        {
            new HashEntry("name", "?¼ì„±?„ìž"),
            new HashEntry("exchange", "KOSPI"),
            new HashEntry("currency", "KRW")
        };

        _mockDatabase.Setup(db => db.HashGetAllAsync(
            It.Is<RedisKey>(k => k.ToString() == $"cache:master:{ticker}"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(hashEntries);

        // Act
        var result = await _repository.GetByTickerAsync(ticker);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ticker, result.Ticker);
        Assert.AreEqual("?¼ì„±?„ìž", result.Name);
        Assert.AreEqual("KOSPI", result.Exchange);
        Assert.AreEqual(CurrencyType.KRW, result.Currency);
    }

    [TestMethod]
    public async Task GetByTickerAsync_WithNonExistingTicker_ShouldReturnNull()
    {
        // Arrange
        var ticker = "INVALID";
        _mockDatabase.Setup(db => db.HashGetAllAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        // Act
        var result = await _repository.GetByTickerAsync(ticker);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetByTickerAsync_WithInvalidCurrency_ShouldDefaultToUSD()
    {
        // Arrange
        var ticker = "TEST";
        var hashEntries = new HashEntry[]
        {
            new HashEntry("name", "Test Stock"),
            new HashEntry("exchange", "TEST"),
            new HashEntry("currency", "INVALID_CURRENCY")
        };

        _mockDatabase.Setup(db => db.HashGetAllAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(hashEntries);

        // Act
        var result = await _repository.GetByTickerAsync(ticker);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(CurrencyType.USD, result.Currency);
    }
}
