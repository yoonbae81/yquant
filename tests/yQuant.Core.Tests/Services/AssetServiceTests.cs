using Moq;
using Xunit;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Services;

namespace yQuant.Core.Tests.Services;

public class AssetServiceTests
{
    private readonly Mock<IBrokerAdapterFactory> _mockAdapterFactory;
    private readonly Mock<IBrokerAdapter> _mockBrokerAdapter;
    private readonly AssetService _assetService;

    public AssetServiceTests()
    {
        _mockAdapterFactory = new Mock<IBrokerAdapterFactory>();
        _mockBrokerAdapter = new Mock<IBrokerAdapter>();
        _assetService = new AssetService(_mockAdapterFactory.Object);
    }

    [Fact]
    public async Task GetAccountOverviewAsync_ShouldReturnAccountWithPositions()
    {
        // Arrange
        var accountNumber = "12345678-01";
        var accountAlias = "user1";
        var expectedAccount = new Account
        {
            Alias = accountAlias,
            Number = accountNumber,
            Broker = "TestBroker",
            AppKey = "test_key",
            AppSecret = "test_secret",
            Active = true,
            Deposits = new Dictionary<CurrencyType, decimal>
            {
                { CurrencyType.KRW, 1000000 },
                { CurrencyType.USD, 1000 }
            }
        };

        var expectedPositions = new List<Position>
        {
            new Position
            {
                AccountAlias = accountAlias,
                Ticker = "005930",
                Currency = CurrencyType.KRW,
                Qty = 10,
                AvgPrice = 70000,
                CurrentPrice = 75000,
                BuyReason = "Domestic"
            },
            new Position
            {
                AccountAlias = accountAlias,
                Ticker = "AAPL",
                Currency = CurrencyType.USD,
                Qty = 5,
                AvgPrice = 150,
                CurrentPrice = 180,
                BuyReason = "Overseas"
            }
        };

        _mockAdapterFactory.Setup(x => x.GetAdapter(accountAlias))
            .Returns(_mockBrokerAdapter.Object);

        _mockBrokerAdapter.Setup(x => x.GetDepositAsync(null, false))
            .ReturnsAsync(expectedAccount);

        _mockBrokerAdapter.Setup(x => x.GetPositionsAsync())
            .ReturnsAsync(expectedPositions);

        // Act
        var result = await _assetService.GetAccountOverviewAsync(accountAlias);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountNumber, result.Number);
        Assert.Equal(2, result.Deposits.Count);
        Assert.Equal(1000000, result.Deposits[CurrencyType.KRW]);
        Assert.Equal(1000, result.Deposits[CurrencyType.USD]);

        Assert.NotNull(result.Positions);
        Assert.Equal(2, result.Positions.Count);
        Assert.True(result.Positions.ContainsKey("005930"));
        Assert.True(result.Positions.ContainsKey("AAPL"));
        Assert.Equal(10, result.Positions["005930"][0].Qty);
        Assert.Equal(5, result.Positions["AAPL"][0].Qty);

        _mockAdapterFactory.Verify(x => x.GetAdapter(accountAlias), Times.Once);
        _mockBrokerAdapter.Verify(x => x.GetDepositAsync(null, false), Times.Once);
        _mockBrokerAdapter.Verify(x => x.GetPositionsAsync(), Times.Once);
    }
}
