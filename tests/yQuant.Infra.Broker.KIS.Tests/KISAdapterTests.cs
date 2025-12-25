using Moq;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Broker.KIS.Models;
using Xunit;

namespace yQuant.Infra.Broker.KIS.Tests;

public class KISAdapterTests
{
    private readonly Mock<IKISClient> _mockKisClient;
    private readonly Mock<ILogger<KISBrokerAdapter>> _mockLogger;
    private readonly KISBrokerAdapter _adapter;
    private const string AccountNoPrefix = "12345678";
    private const string UserId = "test_user";
    private const string AccountAlias = "test_alias";
    private const string AccountNumber = "12345678-01";

    public KISAdapterTests()
    {
        _mockKisClient = new Mock<IKISClient>();
        _mockLogger = new Mock<ILogger<KISBrokerAdapter>>();

        var account = new Account
        {
            Alias = AccountAlias,
            Number = AccountNumber,
            Broker = "KIS",
            AppKey = "test_key",
            AppSecret = "test_secret",
            Deposits = new Dictionary<CurrencyType, decimal>(),
            Active = true
        };
        _mockKisClient.Setup(c => c.Account).Returns(account);

        _adapter = new KISBrokerAdapter(_mockKisClient.Object, _mockLogger.Object);
    }

    [Fact(Skip = "Requires API configuration")]
    public async Task PlaceOrderAsync_ShouldCallExecuteAsync_WithCorrectParams()
    {
        // Arrange
        var order = new Order
        {
            AccountAlias = AccountAlias,
            Ticker = "005930",
            Action = OrderAction.Buy,
            Type = OrderType.Limit,
            Qty = 10,
            Price = 70000
        };

        _mockKisClient
            .Setup(c => c.ExecuteAsync<DomesticOrderResponse>(
                "DomesticBuyOrder", // Endpoint name changed in Adapter logic
                It.IsAny<object>(),
                null,
                null,
                null
            ))
            .ReturnsAsync(new DomesticOrderResponse
            {
                RtCd = "0",
                Msg1 = "Order Placed",
                Output = new DomesticOrderOutput { Odno = "12345" }
            });

        // Act
        var result = await _adapter.PlaceOrderAsync(order);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Order Placed", result.Message);
        Assert.Equal("12345", result.BrokerOrderId);
        _mockKisClient.Verify(c => c.ExecuteAsync<DomesticOrderResponse>(
            "DomesticBuyOrder",
            It.Is<object>(body =>
                body.GetType().GetProperty("PDNO")!.GetValue(body)!.ToString() == "005930" &&
                body.GetType().GetProperty("ORD_QTY")!.GetValue(body)!.ToString() == "10" &&
                body.GetType().GetProperty("ORD_UNPR")!.GetValue(body)!.ToString() == "70000"
            ),
            null,
            null,
            null
        ), Times.Once);
    }

    [Fact(Skip = "Requires API configuration")]
    public async Task PlaceOrderAsync_ShouldReturnErrorMessage_WhenApiFails()
    {
        // Arrange
        var order = new Order
        {
            AccountAlias = AccountAlias,
            Ticker = "005930",
            Action = OrderAction.Buy,
            Type = OrderType.Limit,
            Qty = 10,
            Price = 70000
        };

        _mockKisClient
            .Setup(c => c.ExecuteAsync<DomesticOrderResponse>(
                "DomesticBuyOrder",
                It.IsAny<object>(),
                null,
                null,
                null
            ))
            .ReturnsAsync(new DomesticOrderResponse { RtCd = "1", Msg1 = "Error" });

        // Act
        var result = await _adapter.PlaceOrderAsync(order);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Error (Code: 1)", result.Message);
    }

    [Fact(Skip = "Requires API configuration")]
    public async Task GetPriceAsync_ShouldReturnPrice_WhenDomestic()
    {
        // Arrange
        var ticker = "005930";
        _mockKisClient
            .Setup(c => c.ExecuteAsync<DomesticPriceResponse>(
                "DomesticPrice",
                null,
                It.Is<Dictionary<string, string>>(q => q["FID_INPUT_ISCD"] == ticker),
                null,
                null
            ))
            .ReturnsAsync(new DomesticPriceResponse
            {
                Output = new DomesticPriceDetail
                {
                    StckPrpr = "70000",
                    PrdyCtrt = "1.5"
                }
            });

        // Act
        var result = await _adapter.GetPriceAsync(ticker);

        // Assert
        Assert.Equal(70000m, result.CurrentPrice);
        Assert.Equal(1.5m, result.ChangeRate);
    }

    [Fact(Skip = "Requires API configuration")]
    public async Task GetPriceAsync_ShouldReturnPrice_WhenOverseas()
    {
        // Arrange
        var ticker = "AAPL";
        _mockKisClient
            .Setup(c => c.ExecuteAsync<OverseasPriceResponse>(
                "OverseasPrice",
                null,
                It.Is<Dictionary<string, string>>(q => q["SYMB"] == ticker && q["EXCD"] == "NAS"),
                null,
                null
            ))
            .ReturnsAsync(new OverseasPriceResponse
            {
                Output = new OverseasPriceDetail
                {
                    Last = "150.00",
                    Rate = "2.5"
                }
            });

        // Act
        var result = await _adapter.GetPriceAsync(ticker);

        // Assert
        Assert.Equal(150.00m, result.CurrentPrice);
        Assert.Equal(2.5m, result.ChangeRate);
    }
}