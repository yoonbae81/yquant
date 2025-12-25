using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Infra.Notification.Telegram;

namespace yQuant.Infra.Notification.Telegram.Tests;

public class TelegramNotificationServiceTests
{
    private readonly Mock<ILogger<TelegramNotificationService>> _mockLogger;
    private readonly Mock<IRedisService> _mockRedis;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly IConfiguration _configuration;
    private readonly TelegramNotificationService _service;

    public TelegramNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<TelegramNotificationService>>();
        _mockRedis = new Mock<IRedisService>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        var inMemorySettings = new Dictionary<string, string> {
            {"Notifier:Telegram:BotToken", "test-token"},
            {"Notifier:Telegram:ChatId", "123456"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _service = new TelegramNotificationService(_mockLogger.Object, _configuration, httpClient, _mockRedis.Object);
    }

    [Fact]
    public async Task SendNotificationAsync_SendsMessage_WhenNotDuplicate()
    {
        // Arrange
        var message = "Test Message";
        _mockRedis.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        // Act
        await _service.SendNotificationAsync(message);

        // Assert
        _mockRedis.Verify(r => r.SetAsync(It.IsAny<string>(), "1", It.IsAny<TimeSpan?>()), Times.Once);

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("sendMessage")),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task SendNotificationAsync_SkipsMessage_WhenDuplicate()
    {
        // Arrange
        var message = "Test Message";
        _mockRedis.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        await _service.SendNotificationAsync(message);

        // Assert
        _mockRedis.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}
