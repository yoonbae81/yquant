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

namespace yQuant.App.BrokerGateway.Tests;

[TestClass]
public class BrokerGatewayTests
{
    private Mock<ILogger<Worker>>? _loggerMock;
    private Mock<IConfiguration>? _configMock;
    private Mock<IConnectionMultiplexer>? _redisMock;
    private Mock<ISubscriber>? _subscriberMock;
    private Mock<IServiceProvider>? _serviceProviderMock;
    private Mock<INotificationService>? _telegramNotifierMock;
    private Mock<ITradingLogger>? _tradingLoggerMock;
    private Mock<ISystemLogger>? _systemLoggerMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<Worker>>();
        _configMock = new Mock<IConfiguration>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _subscriberMock = new Mock<ISubscriber>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _telegramNotifierMock = new Mock<INotificationService>();
        _tradingLoggerMock = new Mock<yQuant.Core.Ports.Output.Infrastructure.ITradingLogger>();
        _systemLoggerMock = new Mock<yQuant.Core.Ports.Output.Infrastructure.ISystemLogger>();

        _redisMock.Setup(r => r.GetSubscriber(null)).Returns(_subscriberMock.Object);

        // Setup KISAccountManager dependencies
        var kisManagerLoggerMock = new Mock<ILogger<KISAccountManager>>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var redisServiceMock = new Mock<yQuant.Infra.Middleware.Redis.Interfaces.IRedisService>();

        // Create KISAccountManager instance
        var kisAccountManager = new KISAccountManager(
            kisManagerLoggerMock.Object,
            httpClientFactoryMock.Object,
            _serviceProviderMock.Object,
            redisServiceMock.Object
        );

        // Setup ServiceProvider to return KISAccountManager
        _serviceProviderMock.Setup(x => x.GetService(typeof(KISAccountManager)))
            .Returns(kisAccountManager);
    }

    [TestMethod]
    public async Task Worker_StartsAndStops_Successfully()
    {
        // Arrange
        var templateService = new yQuant.Infra.Notification.Common.Services.TemplateService();
        var telegramBuilder = new TelegramMessageBuilder(templateService);
        var worker = new Worker(_loggerMock!.Object, _configMock!.Object, _redisMock!.Object, _serviceProviderMock!.Object, _telegramNotifierMock!.Object, telegramBuilder, new[] { _tradingLoggerMock!.Object }, _systemLoggerMock!.Object);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // No exceptions were thrown
    }
}