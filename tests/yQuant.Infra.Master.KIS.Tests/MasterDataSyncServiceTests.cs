using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Master.KIS;

namespace yQuant.Infra.Master.KIS.Tests;

[TestClass]
public class MasterDataSyncServiceTests
{
    private Mock<IMasterDataLoader> _mockLoader = null!;
    private Mock<IMasterDataRepository> _mockRepository = null!;
    private Mock<ISystemLogger> _mockSystemLogger = null!;
    private Mock<ILogger<MasterDataSyncService>> _mockLogger = null!;
    private MasterDataSyncService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLoader = new Mock<IMasterDataLoader>();
        _mockRepository = new Mock<IMasterDataRepository>();
        _mockSystemLogger = new Mock<ISystemLogger>();
        _mockLogger = new Mock<ILogger<MasterDataSyncService>>();
        
        _service = new MasterDataSyncService(
            _mockLoader.Object,
            _mockRepository.Object,
            _mockSystemLogger.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task SyncCountryAsync_WithValidData_ShouldLoadAndSaveStocks()
    {
        // Arrange
        var country = "Korea";
        var exchangeUrls = new Dictionary<string, string>
        {
            { "KOSPI", "http://example.com/kospi.zip" },
            { "KOSDAQ", "http://example.com/kosdaq.zip" }
        };

        var kospiStocks = new List<StockMaster>
        {
            new StockMaster { Ticker = "005930", Name = "삼성전자", Exchange = "KOSPI", Currency = CurrencyType.KRW },
            new StockMaster { Ticker = "000660", Name = "SK하이닉스", Exchange = "KOSPI", Currency = CurrencyType.KRW }
        };

        var kosdaqStocks = new List<StockMaster>
        {
            new StockMaster { Ticker = "035420", Name = "NAVER", Exchange = "KOSDAQ", Currency = CurrencyType.KRW }
        };

        _mockLoader.Setup(l => l.LoadMasterDataAsync("KOSPI", "http://example.com/kospi.zip"))
            .ReturnsAsync(kospiStocks);
        _mockLoader.Setup(l => l.LoadMasterDataAsync("KOSDAQ", "http://example.com/kosdaq.zip"))
            .ReturnsAsync(kosdaqStocks);

        // Act
        await _service.SyncCountryAsync(country, exchangeUrls);

        // Assert
        _mockLoader.Verify(l => l.LoadMasterDataAsync("KOSPI", "http://example.com/kospi.zip"), Times.Once);
        _mockLoader.Verify(l => l.LoadMasterDataAsync("KOSDAQ", "http://example.com/kosdaq.zip"), Times.Once);
        
        _mockRepository.Verify(r => r.SaveBatchAsync(
            It.Is<IEnumerable<StockMaster>>(stocks => stocks.Count() == 3),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSystemLogger.Verify(sl => sl.LogStatusAsync(
            "StockMaster",
            It.Is<string>(msg => msg.Contains("Korea"))), Times.Once);
    }

    [TestMethod]
    public async Task SyncCountryAsync_WithNoStocks_ShouldLogError()
    {
        // Arrange
        var country = "TestCountry";
        var exchangeUrls = new Dictionary<string, string>
        {
            { "TEST", "http://example.com/test.zip" }
        };

        _mockLoader.Setup(l => l.LoadMasterDataAsync("TEST", "http://example.com/test.zip"))
            .ReturnsAsync(new List<StockMaster>());

        // Act
        await _service.SyncCountryAsync(country, exchangeUrls);

        // Assert
        _mockRepository.Verify(r => r.SaveBatchAsync(
            It.IsAny<IEnumerable<StockMaster>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _mockSystemLogger.Verify(sl => sl.LogSystemErrorAsync(
            It.Is<string>(title => title.Contains("Failed")),
            It.IsAny<Exception>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncCountryAsync_WithEmptyUrls_ShouldSkip()
    {
        // Arrange
        var country = "EmptyCountry";
        var exchangeUrls = new Dictionary<string, string>();

        // Act
        await _service.SyncCountryAsync(country, exchangeUrls);

        // Assert
        _mockLoader.Verify(l => l.LoadMasterDataAsync(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        _mockRepository.Verify(r => r.SaveBatchAsync(
            It.IsAny<IEnumerable<StockMaster>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SyncCountryAsync_WithRepositoryFailure_ShouldLogError()
    {
        // Arrange
        var country = "Korea";
        var exchangeUrls = new Dictionary<string, string>
        {
            { "KOSPI", "http://example.com/kospi.zip" }
        };

        var stocks = new List<StockMaster>
        {
            new StockMaster { Ticker = "005930", Name = "삼성전자", Exchange = "KOSPI", Currency = CurrencyType.KRW }
        };

        _mockLoader.Setup(l => l.LoadMasterDataAsync("KOSPI", "http://example.com/kospi.zip"))
            .ReturnsAsync(stocks);
        _mockRepository.Setup(r => r.SaveBatchAsync(It.IsAny<IEnumerable<StockMaster>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        // Act
        await _service.SyncCountryAsync(country, exchangeUrls);

        // Assert
        _mockSystemLogger.Verify(sl => sl.LogSystemErrorAsync(
            It.Is<string>(title => title.Contains("Save Failed")),
            It.IsAny<Exception>()), Times.Once);
    }

    [TestMethod]
    public async Task SyncAllAsync_ShouldSyncAllCountries()
    {
        // Arrange
        var countries = new Dictionary<string, Dictionary<string, string>>
        {
            { "Korea", new Dictionary<string, string> { { "KOSPI", "http://example.com/kospi.zip" } } },
            { "USA", new Dictionary<string, string> { { "NASDAQ", "http://example.com/nasdaq.zip" } } }
        };

        var stocks = new List<StockMaster>
        {
            new StockMaster { Ticker = "TEST", Name = "Test Stock", Exchange = "TEST", Currency = CurrencyType.USD }
        };

        _mockLoader.Setup(l => l.LoadMasterDataAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(stocks);

        // Act
        await _service.SyncAllAsync(countries);

        // Assert
        _mockLoader.Verify(l => l.LoadMasterDataAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        _mockRepository.Verify(r => r.SaveBatchAsync(It.IsAny<IEnumerable<StockMaster>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
