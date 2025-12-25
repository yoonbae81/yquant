using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Broker.KIS.Models;
using yQuant.Infra.Redis.Interfaces;
using Xunit;

namespace yQuant.Infra.Broker.KIS.Tests;

public class KISClientRateLimitTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<KISClient>> _mockLogger;
    private readonly Mock<ITokenRedisService> _mockTokenRedis;
    private readonly KISApiConfig _apiConfig;
    private readonly Account _account;

    public KISClientRateLimitTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<KISClient>>();
        _mockTokenRedis = new Mock<ITokenRedisService>();

        _apiConfig = new KISApiConfig();
        _apiConfig.ExtensionData["Token"] = JsonSerializer.SerializeToElement(new EndpointConfig { Path = "/oauth2/tokenP", Method = "POST" });
        _apiConfig.ExtensionData["TestEndpoint"] = JsonSerializer.SerializeToElement(new EndpointConfig { Path = "/test", Method = "GET" });

        _account = new Account
        {
            Alias = "test_account",
            Number = "12345678-01",
            Broker = "KIS",
            AppKey = "test_app_key",
            AppSecret = "test_app_secret",
            Deposits = new Dictionary<CurrencyType, decimal>(),
            Active = true
        };
    }

    [Fact]
    public async Task RateLimit_ShouldAllowRequestsUpToLimit()
    {
        // Arrange
        int rateLimit = 5;
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api.com")
        };

        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                    {
                        AccessToken = "test_token",
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/test"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"result\":\"ok\"}")
                };
            }
            return null;
        });

        var client = new KISClient(httpClient, _mockLogger.Object, _account, _apiConfig, "https://api.test.com", _mockTokenRedis.Object, rateLimit);

        // Act - Make requests up to the limit
        var tasks = new List<Task>();
        for (int i = 0; i < rateLimit; i++)
        {
            tasks.Add(client.ExecuteAsync<object>("TestEndpoint"));
        }

        // Assert - All requests should complete successfully
        await Task.WhenAll(tasks);
        Assert.Equal(rateLimit, tasks.Count(t => t.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task RateLimit_ShouldThrowException_WhenExceedingLimit()
    {
        // Arrange
        int rateLimit = 3;
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api.com")
        };

        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                    {
                        AccessToken = "test_token",
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/test"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"result\":\"ok\"}")
                };
            }
            return null;
        });

        var client = new KISClient(httpClient, _mockLogger.Object, _account, _apiConfig, "https://api.test.com", _mockTokenRedis.Object, rateLimit);

        // Act & Assert
        // The rate limiter has QueueLimit=100, so we need to exceed that to get exceptions
        // We'll fire 110 requests (3 permit + 100 queue + 7 overflow)
        var tasks = new List<Task>();
        for (int i = 0; i < 110; i++)
        {
            tasks.Add(client.ExecuteAsync<object>("TestEndpoint"));
        }

        // Some tasks should throw RateLimitExceededException
        var exceptions = new List<Exception>();
        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        Assert.NotEmpty(exceptions);
        Assert.Contains(exceptions, ex => ex is RateLimitExceededException);
    }

    [Fact]
    public async Task RateLimit_ShouldResetAfterTimeWindow()
    {
        // Arrange
        int rateLimit = 5;
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api.com")
        };

        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                    {
                        AccessToken = "test_token",
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/test"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"result\":\"ok\"}")
                };
            }
            return null;
        });

        var client = new KISClient(httpClient, _mockLogger.Object, _account, _apiConfig, "https://api.test.com", _mockTokenRedis.Object, rateLimit);

        // Act - First batch
        var firstBatch = new List<Task>();
        for (int i = 0; i < rateLimit; i++)
        {
            firstBatch.Add(client.ExecuteAsync<object>("TestEndpoint"));
        }
        await Task.WhenAll(firstBatch);

        // Wait for rate limit window to reset (1 second + buffer)
        await Task.Delay(1100);

        // Second batch - should succeed after window reset
        var secondBatch = new List<Task>();
        for (int i = 0; i < rateLimit; i++)
        {
            secondBatch.Add(client.ExecuteAsync<object>("TestEndpoint"));
        }

        // Assert - All requests in second batch should complete successfully
        await Task.WhenAll(secondBatch);
        Assert.Equal(rateLimit, secondBatch.Count(t => t.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task RateLimit_ShouldUseDefaultValue_WhenNotSpecified()
    {
        // Arrange
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api.com")
        };

        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                    {
                        AccessToken = "test_token",
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/test"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"result\":\"ok\"}")
                };
            }
            return null;
        });

        // Act - Create client without specifying rate limit (should use default 20)
        var client = new KISClient(httpClient, _mockLogger.Object, _account, _apiConfig, "https://api.test.com", _mockTokenRedis.Object);

        // Make 20 requests (default limit)
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(client.ExecuteAsync<object>("TestEndpoint"));
        }

        // Assert - All 20 requests should complete successfully
        await Task.WhenAll(tasks);
        Assert.Equal(20, tasks.Count(t => t.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task RateLimit_ShouldMeasureActualThroughput()
    {
        // Arrange
        int rateLimit = 10;
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api.com")
        };

        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                    {
                        AccessToken = "test_token",
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/test"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"result\":\"ok\"}")
                };
            }
            return null;
        });

        var client = new KISClient(httpClient, _mockLogger.Object, _account, _apiConfig, "https://api.test.com", _mockTokenRedis.Object, rateLimit);

        // Act - Measure time to complete rate limit requests
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();
        for (int i = 0; i < rateLimit; i++)
        {
            tasks.Add(client.ExecuteAsync<object>("TestEndpoint"));
        }
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - Should complete within reasonable time (< 2 seconds for 10 requests at 10/sec)
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Expected completion within 2000ms, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    private void SetupMockResponse(Func<HttpRequestMessage, HttpResponseMessage?> responseProvider)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns((HttpRequestMessage req, CancellationToken token) =>
            {
                var response = responseProvider(req);
                if (response != null)
                {
                    return Task.FromResult(response);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = req });
            });
    }
}
