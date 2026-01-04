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
using yQuant.App.Webhook.Models;
using yQuant.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Ports.Output.Infrastructure;
using System.Collections.Generic;

namespace yQuant.App.Webhook.Tests;

[TestClass]
public class TradingViewWebhookTests
{
    private WebApplicationFactory<Program>? _factory;
    private Mock<IConnectionMultiplexer>? _redisMock;
    private Mock<IDatabase>? _dbMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Allow any IP for tests
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"Webhook:AllowedIps:0", "127.0.0.1"},
                        {"Webhook:Secrets:TradingView", "test-secret"}
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    // Remove the real Valkey connection and add mock
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddSingleton(_redisMock.Object);
                    services.AddSingleton(new Mock<ITradingLogger>().Object);
                    services.AddSingleton(new Mock<ISystemLogger>().Object);
                });
            });
    }

    [TestMethod]
    [Ignore("Requires full application configuration including Valkey connection string")]
    public async Task Webhook_WithValidPayload_ReturnsOkAndPublishesToValkey()
    {
        // Arrange
        var client = _factory!.CreateClient();
        var payload = new TradingViewPayload
        {
            Ticker = "AAPL",
            Action = "Buy",
            Price = 150.0m,
            Exchange = "NASDAQ",
            Currency = "USD",
            Strategy = "TestStrategy",
            Secret = "test-secret" // Matches configuration in TestInitialize
        };
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/webhook", content);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        _dbMock!.Verify(
            db => db.PublishAsync(
                It.Is<ValkeyChannel>(c => c == "signal"),
                It.IsAny<ValkeyValue>(),
                CommandFlags.None),
            Times.Once);
    }

    [TestMethod]
    [Ignore("Requires full application configuration including Valkey connection string")]
    public async Task Webhook_WithInvalidSecret_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory!.CreateClient();
        var payload = new TradingViewPayload
        {
            Ticker = "AAPL",
            Action = "Buy",
            Price = 150.0m,
            Exchange = "NASDAQ",
            Currency = "USD",
            Strategy = "TestStrategy",
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