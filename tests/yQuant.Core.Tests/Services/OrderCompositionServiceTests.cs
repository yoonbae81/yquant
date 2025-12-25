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
    private readonly Mock<IStrategyPolicyMapper> _mockPolicyMapper;
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<IStrategyAccountMapper> _mockAccountMapper;
    private readonly Mock<IOrderPublisher> _mockOrderPublisher;
    private readonly Mock<ILogger<OrderCompositionService>> _mockLogger;
    private readonly OrderCompositionService _service;

    public OrderCompositionServiceTests()
    {
        _mockMarketRule = new Mock<IMarketRule>();
        _mockPositionSizer = new Mock<IPositionSizer>();
        // Mock the type name behavior if needed, but our implementation uses StartsWith(policyName)
        // BasicPositionSizer will match "Basic"

        _mockPolicyMapper = new Mock<IStrategyPolicyMapper>();
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockAccountMapper = new Mock<IStrategyAccountMapper>();
        _mockOrderPublisher = new Mock<IOrderPublisher>();
        _mockLogger = new Mock<ILogger<OrderCompositionService>>();

        _service = new OrderCompositionService(
            new[] { _mockMarketRule.Object },
            new[] { _mockPositionSizer.Object },
            _mockPolicyMapper.Object,
            _mockAccountRepository.Object,
            _mockAccountMapper.Object,
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
            Strategy = "Strategy1"
        };
        var accountAlias = "ACC-001";
        var account = new Account
        {
            Alias = accountAlias,
            Number = "12345",
            Broker = "Test Broker",
            AppKey = "test_key",
            AppSecret = "test_secret",
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

        // We need the sizer's type name to start with the policy name.
        // For testing, we can use a concrete mock or just adjust the implementation later.
        // However, IPositionSizer is an interface, so its type name is like "Castle.Proxies.IPositionSizerProxy".
        // Let's adjust the test to use a policy name that matches the mock's type or use a simple fake.
        _mockPolicyMapper.Setup(m => m.GetSizingPolicyName("Strategy1")).Returns("Fake"); // Policy name set to "Fake"

        _mockAccountMapper.Setup(r => r.GetAccountAliasesForStrategy("Strategy1")).Returns(new[] { accountAlias });
        _mockAccountRepository.Setup(r => r.GetAccountAsync(accountAlias)).ReturnsAsync(account);

        // Setup a service with the fake sizer for this specific test
        var fakeSizer = new FakePositionSizer(); // Use the concrete FakePositionSizer
        var serviceWithFakeSizer = new OrderCompositionService(
            new[] { _mockMarketRule.Object },
            new[] { fakeSizer }, // Pass the concrete FakePositionSizer
            _mockPolicyMapper.Object,
            _mockAccountRepository.Object,
            _mockAccountMapper.Object,
            _mockOrderPublisher.Object,
            _mockLogger.Object
        );

        // Configure the fake sizer's behavior
        fakeSizer.CalculatePositionSizeFunc = (s, a) => order;
        fakeSizer.ValidateOrderFunc = (Order o, Account a, out string reason) => { reason = ""; return true; };

        // Act
        await serviceWithFakeSizer.ProcessSignalAsync(signal); // Use the service with the fake sizer

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
            Strategy = "Strategy1"
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
            Strategy = "Strategy1"
        };

        _mockMarketRule.Setup(r => r.CanHandle("NASDAQ")).Returns(true);
        _mockMarketRule.Setup(r => r.IsMarketOpen(It.IsAny<DateTime>())).Returns(true);
        _mockMarketRule.Setup(r => r.GetCurrency()).Returns(CurrencyType.USD);

        _mockPolicyMapper.Setup(m => m.GetSizingPolicyName("Strategy1")).Returns("Fake");

        _mockAccountMapper.Setup(r => r.GetAccountAliasesForStrategy("Strategy1")).Returns(new[] { "ACC-001" });
        _mockAccountRepository.Setup(r => r.GetAccountAsync("ACC-001")).ReturnsAsync((Account?)null);

        var fakeSizer = new FakePositionSizer();
        var serviceWithFake = new OrderCompositionService(
            new[] { _mockMarketRule.Object },
            new[] { fakeSizer },
            _mockPolicyMapper.Object,
            _mockAccountRepository.Object,
            _mockAccountMapper.Object,
            _mockOrderPublisher.Object,
            _mockLogger.Object
        );

        // Act
        await serviceWithFake.ProcessSignalAsync(signal);

        // Assert
        _mockOrderPublisher.Verify(p => p.PublishOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    private class FakePositionSizer : IPositionSizer
    {
        public Func<Signal, Account, Order?>? CalculatePositionSizeFunc { get; set; }
        public delegate bool ValidateOrderDelegate(Order order, Account account, out string failureReason);
        public ValidateOrderDelegate? ValidateOrderFunc { get; set; }

        public Order? CalculatePositionSize(Signal signal, Account account)
            => CalculatePositionSizeFunc?.Invoke(signal, account);

        public bool ValidateOrder(Order order, Account account, out string failureReason)
        {
            if (ValidateOrderFunc != null)
            {
                return ValidateOrderFunc(order, account, out failureReason);
            }
            failureReason = "";
            return true;
        }
    }
}
