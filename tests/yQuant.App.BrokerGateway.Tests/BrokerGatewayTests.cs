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

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<Worker>>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _subscriberMock = new Mock<ISubscriber>();
        _telegramNotifierMock = new Mock<INotificationService>();
        _tradingLoggerMock = new Mock<ITradingLogger>();

        _redisMock.Setup(r => r.GetSubscriber(null)).Returns(_subscriberMock.Object);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(new Mock<IDatabase>().Object);
    }

    [TestMethod]
    public async Task Worker_StartsAndStops_Successfully()
    {
        // Arrange
        var templateService = new yQuant.Infra.Notification.Telegram.Services.TelegramTemplateService();
        var telegramBuilder = new TelegramMessageBuilder(templateService);
        var adapters = new Dictionary<string, IBrokerAdapter>();

        var worker = new Worker(
            _loggerMock!.Object,
            _redisMock!.Object,
            adapters,
            _telegramNotifierMock!.Object,
            telegramBuilder,
            new[] { _tradingLoggerMock!.Object });

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // No exceptions were thrown
    }
}