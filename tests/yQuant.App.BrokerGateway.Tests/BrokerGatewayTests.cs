using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using yQuant.App.BrokerGateway;
using StackExchange.Redis;
using System.Threading.Tasks;
using System.Threading;
using yQuant.Infra.Notification.Telegram;
using yQuant.Core.Ports.Output.Infrastructure;
using System.Collections.Generic;

namespace yQuant.App.BrokerGateway.Tests;

[TestClass]
public class BrokerGatewayTests
{
    private Mock<ILogger<Worker>>? _loggerMock;
    private Mock<IConnectionMultiplexer>? _redisMock;
    private Mock<ISubscriber>? _subscriberMock;
    private Mock<INotificationService>? _telegramNotifierMock;
    private Mock<ITradingLogger>? _tradingLoggerMock;
    private Mock<ITradeRepository>? _tradeRepositoryMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<Worker>>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _subscriberMock = new Mock<ISubscriber>();
        _telegramNotifierMock = new Mock<INotificationService>();
        _tradingLoggerMock = new Mock<ITradingLogger>();
        _tradeRepositoryMock = new Mock<ITradeRepository>();

        _redisMock.Setup(r => r.GetSubscriber(null)).Returns(_subscriberMock.Object);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(new Mock<IDatabase>().Object);
    }

    [TestMethod]
    [Ignore("RegisterAccountsAsync functionality has been removed/refactored")]
    public async Task Worker_RegistersAccounts_OnStartup()
    {
        // Arrange
        var templateService = new yQuant.Infra.Notification.Telegram.Services.TelegramTemplateService();
        var telegramBuilder = new TelegramMessageBuilder(templateService);

        var mockAdapter = new Mock<IBrokerAdapter>();
        var adapters = new Dictionary<string, IBrokerAdapter>
        {
            { "TestAccount1", mockAdapter.Object },
            { "TestAccount2", mockAdapter.Object }
        };

        var mockDatabase = new Mock<IDatabase>();
        _redisMock!.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var mockConfiguration = new Mock<IConfiguration>();

        var worker = new Worker(
            _loggerMock!.Object,
            _redisMock!.Object,
            adapters,
            _telegramNotifierMock!.Object,
            telegramBuilder,
            new[] { _tradingLoggerMock!.Object },
            _tradeRepositoryMock!.Object,
            mockConfiguration.Object);

        // Act
        // StartAsync calls RegisterAccountsAsync immediately now
        await worker.StartAsync(CancellationToken.None);

        // Cleanup - stop the worker to avoid background tasks running
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Verify StringSetAsync was called with the correct key and JSON containing the account aliases
        mockDatabase.Verify(db => db.StringSetAsync(
            "broker:accounts",
            It.Is<RedisValue>(v => v.ToString().Contains("TestAccount1") && v.ToString().Contains("TestAccount2")),
            It.IsAny<TimeSpan?>(),
            false,
            When.Always,
            CommandFlags.None), Times.Once);
    }

    [TestMethod]
    public async Task Worker_StartsAndStops_Successfully()
    {
        // Arrange
        var templateService = new yQuant.Infra.Notification.Telegram.Services.TelegramTemplateService();
        var telegramBuilder = new TelegramMessageBuilder(templateService);
        var adapters = new Dictionary<string, IBrokerAdapter>();

        var mockConfiguration = new Mock<IConfiguration>();

        var worker = new Worker(
            _loggerMock!.Object,
            _redisMock!.Object,
            adapters,
            _telegramNotifierMock!.Object,
            telegramBuilder,
            new[] { _tradingLoggerMock!.Object },
            _tradeRepositoryMock!.Object,
            mockConfiguration.Object);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // No exceptions were thrown
    }
}