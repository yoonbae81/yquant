using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using yQuant.App.TradingViewWebhook.Models;
using yQuant.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace yQuant.App.TradingViewWebhook.Tests;

[TestClass]
public class TradingViewWebhookTests
{
    private WebApplicationFactory<Program> _factory;
    private Mock<IConnectionMultiplexer> _redisMock;
    private Mock<IDatabase> _dbMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(_redisMock.Object);
                });
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Allow any IP for tests
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"Security:AllowedIps:0", "127.0.0.1"},
                        {"Security:WebhookSecret", "giC3CNMLMsQ0JlPDUreQ"} 
                    });
                });
            });
    }

    [TestMethod]
    public async Task Webhook_WithValidPayload_ReturnsOkAndPublishesToRedis()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new TradingViewPayload
        {
            Ticker = "AAPL",
            Action = "Buy",
            Price = 150.0m,
            Exchange = "NASDAQ",
            Currency = "USD",
            Comment = "TestStrategy",
            Secret = "giC3CNMLMsQ0JlPDUreQ" // From appsettings.json
        };
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/webhook", content);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        _dbMock.Verify(
            db => db.PublishAsync(
                It.Is<RedisChannel>(c => c == "signal"),
                It.IsAny<RedisValue>(),
                CommandFlags.None),
            Times.Once);
    }

    [TestMethod]
    public async Task Webhook_WithInvalidSecret_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new TradingViewPayload
        {
            Ticker = "AAPL",
            Action = "Buy",
            Price = 150.0m,
            Exchange = "NASDAQ",
            Currency = "USD",
            Comment = "TestStrategy",
            Secret = "wrong-secret"
        };
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/webhook", content);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}