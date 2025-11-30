using Moq;
using Xunit;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Services;

namespace yQuant.Core.Tests.Services;

public class AssetServiceTests
{
    private readonly Mock<IBrokerAdapter> _mockBrokerAdapter;
    private readonly AssetService _assetService;

    public AssetServiceTests()
    {
        _mockBrokerAdapter = new Mock<IBrokerAdapter>();
        _assetService = new AssetService(_mockBrokerAdapter.Object);
    }

    [Fact]
    public async Task GetAccountOverviewAsync_ShouldReturnAccountWithPositions()
    {
        // Arrange
        var accountNumber = "12345678-01";
        var expectedAccount = new Account
        {
            Alias = "user1",
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
                AccountAlias = "TestAccount",
                Ticker = "005930",
                Currency = CurrencyType.KRW,
                Qty = 10,
                AvgPrice = 70000,
                CurrentPrice = 75000,
                Source = "Domestic"
            },
            new Position
            {
                AccountAlias = "TestAccount",
                Ticker = "AAPL",
                Currency = CurrencyType.USD,
                Qty = 5,
                AvgPrice = 150,
                CurrentPrice = 180,
                Source = "Overseas"
            }
        };

        _mockBrokerAdapter.Setup(x => x.GetAccountStateAsync(accountNumber))
            .ReturnsAsync(expectedAccount);

        _mockBrokerAdapter.Setup(x => x.GetPositionsAsync(accountNumber))
            .ReturnsAsync(expectedPositions);

        // Act
        var result = await _assetService.GetAccountOverviewAsync(accountNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountNumber, result.Number);
        Assert.Equal(2, result.Deposits.Count);
        Assert.Equal(1000000, result.Deposits[CurrencyType.KRW]);
        Assert.Equal(1000, result.Deposits[CurrencyType.USD]);
        
        Assert.NotNull(result.Positions);
        Assert.Equal(2, result.Positions.Count);
        Assert.Contains(result.Positions, p => p.Ticker == "005930" && p.Qty == 10);
        Assert.Contains(result.Positions, p => p.Ticker == "AAPL" && p.Qty == 5);

        _mockBrokerAdapter.Verify(x => x.GetAccountStateAsync(accountNumber), Times.Once);
        _mockBrokerAdapter.Verify(x => x.GetPositionsAsync(accountNumber), Times.Once);
    }
}
