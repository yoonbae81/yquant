using Moq;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Ports.Output.Policies;
using yQuant.Core.Services;

namespace yQuant.Core.Tests.Services;

public class OrderCompositionServiceTests
{
    private readonly Mock<IMarketRule> _mockMarketRule;
    private readonly Mock<IPositionSizer> _mockPositionSizer;
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<IAccountRegistry> _mockAccountRegistry;
    private readonly Mock<IOrderPublisher> _mockOrderPublisher;
    private readonly Mock<ILogger<OrderCompositionService>> _mockLogger;
    private readonly OrderCompositionService _service;

    public OrderCompositionServiceTests()
    {
        _mockMarketRule = new Mock<IMarketRule>();
        _mockPositionSizer = new Mock<IPositionSizer>();
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockAccountRegistry = new Mock<IAccountRegistry>();
        _mockOrderPublisher = new Mock<IOrderPublisher>();
        _mockLogger = new Mock<ILogger<OrderCompositionService>>();

        _service = new OrderCompositionService(
            new[] { _mockMarketRule.Object },
            _mockPositionSizer.Object,
            _mockAccountRepository.Object,
            _mockAccountRegistry.Object,
            _mockOrderPublisher.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ProcessSignalAsync_ShouldPublishOrder_WhenAllConditionsMet()
    {
        // Arrange
        var signal = new Signal 
        { 
            Ticker = "AAPL", 
            Exchange = "NASDAQ", 
            Action = OrderAction.Buy, 
            Price = 150m, 
            Timestamp = DateTime.UtcNow, 
            Source = "Strategy1" 
        };
        var accountAlias = "ACC-001";
        var account = new Account 
        { 
            Id = "USER-001",
            Alias = accountAlias, 
            AccountNumber = "12345", 
            Broker = "Test Broker",
            Active = true,
            Deposits = new Dictionary<CurrencyType, decimal>()
        };
        var order = new Order 
        { 
            AccountAlias = accountAlias, 
            Ticker = "AAPL", 
            Action = OrderAction.Buy, 
            Type = OrderType.Market, 
            Qty = 10, 
            Price = 150m 
        };

        _mockMarketRule.Setup(r => r.CanHandle("NASDAQ")).Returns(true);
        _mockMarketRule.Setup(r => r.IsMarketOpen(It.IsAny<DateTime>())).Returns(true);
        _mockMarketRule.Setup(r => r.GetCurrency()).Returns(CurrencyType.USD);

        _mockAccountRegistry.Setup(r => r.GetAccountAliasForCurrency(CurrencyType.USD)).Returns(accountAlias);
        _mockAccountRepository.Setup(r => r.GetAccountAsync(accountAlias)).ReturnsAsync(account);

        _mockPositionSizer.Setup(s => s.CalculatePositionSize(signal, account)).Returns(order);
        string? failureReason;
        _mockPositionSizer.Setup(s => s.ValidateOrder(order, account, out failureReason)).Returns(true);

        // Act
        await _service.ProcessSignalAsync(signal);

        // Assert
        _mockOrderPublisher.Verify(p => p.PublishOrderAsync(order), Times.Once);
    }

    [Fact]
    public async Task ProcessSignalAsync_ShouldNotPublish_WhenMarketClosed()
    {
        // Arrange
        var signal = new Signal 
        { 
            Ticker = "AAPL", 
            Exchange = "NASDAQ", 
            Action = OrderAction.Buy, 
            Price = 150m, 
            Timestamp = DateTime.UtcNow, 
            Source = "Strategy1" 
        };

        _mockMarketRule.Setup(r => r.CanHandle("NASDAQ")).Returns(true);
        _mockMarketRule.Setup(r => r.IsMarketOpen(It.IsAny<DateTime>())).Returns(false);

        // Act
        await _service.ProcessSignalAsync(signal);

        // Assert
        _mockOrderPublisher.Verify(p => p.PublishOrderAsync(It.IsAny<Order>()), Times.Never);
    }
    
    [Fact]
    public async Task ProcessSignalAsync_ShouldNotPublish_WhenAccountNotFound()
    {
        // Arrange
        var signal = new Signal 
        { 
            Ticker = "AAPL", 
            Exchange = "NASDAQ", 
            Action = OrderAction.Buy, 
            Price = 150m, 
            Timestamp = DateTime.UtcNow, 
            Source = "Strategy1" 
        };

        _mockMarketRule.Setup(r => r.CanHandle("NASDAQ")).Returns(true);
        _mockMarketRule.Setup(r => r.IsMarketOpen(It.IsAny<DateTime>())).Returns(true);
        _mockMarketRule.Setup(r => r.GetCurrency()).Returns(CurrencyType.USD);

        _mockAccountRegistry.Setup(r => r.GetAccountAliasForCurrency(CurrencyType.USD)).Returns("ACC-001");
        _mockAccountRepository.Setup(r => r.GetAccountAsync("ACC-001")).ReturnsAsync((Account?)null);

        // Act
        await _service.ProcessSignalAsync(signal);

        // Assert
        _mockOrderPublisher.Verify(p => p.PublishOrderAsync(It.IsAny<Order>()), Times.Never);
    }
}
