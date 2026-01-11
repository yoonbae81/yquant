using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Broker.KIS.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using Xunit;

namespace yQuant.Infra.Broker.KIS.Tests;

public class KISClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<KISClient>> _mockLogger;
    private readonly Mock<IKisTokenRepository> _mockTokenRepository;
    private readonly KISClient _client;
    private readonly KISApiConfig _apiConfig;
    private readonly string _userId;
    private readonly string _accountAlias;

    public KISClientTests()
    {
        _userId = Guid.NewGuid().ToString();
        _accountAlias = $"test_alias_{Guid.NewGuid()}";
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api.com")
        };

        _mockLogger = new Mock<ILogger<KISClient>>();
        _mockTokenRepository = new Mock<IKisTokenRepository>();

        _apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));
        if (_apiConfig == null || _apiConfig.ExtensionData.Count == 0)
        {
            // Fallback to manual config if API folder doesn't exist
            _apiConfig = new KISApiConfig();
            _apiConfig.ExtensionData["Token"] = JsonSerializer.SerializeToElement(new EndpointConfig { Path = "/oauth2/tokenP", Method = "POST" });
            _apiConfig.ExtensionData["Hashkey"] = JsonSerializer.SerializeToElement(new EndpointConfig { Path = "/uapi/hashkey", Method = "POST" });
            _apiConfig.ExtensionData["Order"] = JsonSerializer.SerializeToElement(new EndpointConfig { Path = "/uapi/domestic-stock/v1/trading/order-cash", Method = "POST", TrId = "TTTC0802U" });
        }


        var account = new yQuant.Core.Models.Account
        {
            Alias = _accountAlias,
            Number = "12345678-01",
            Broker = "KIS",
            AppKey = "test_app_key",
            AppSecret = "test_app_secret",
            Deposits = new Dictionary<CurrencyType, decimal>(),
            Active = true
        };

        _client = new KISClient(
            httpClient,
            _mockLogger.Object,
            account,
            _apiConfig,
            "https://api.test.com",
            _mockTokenRepository.Object
        );
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

    [Fact(Skip = "Requires API configuration files")]
    public async Task ExecuteAsync_ShouldGetToken_WhenNoTokenExists()
    {
        // Arrange
        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                    {
                        AccessToken = "new_access_token",
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { RT_CD = "0", MSG1 = "Success" }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/hashkey"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new HashkeyResponse { HASH = "test_hash" }))
                };
            }
            return null;
        });

        // Act
        await _client.ExecuteAsync<object>("Order", new { test = "body" });

        // Assert
        // Verify Token Request was made
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP")),
            ItExpr.IsAny<CancellationToken>()
        );

        // Verify Order Request has Authorization header
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash") &&
                req.Headers.Authorization!.Parameter == "new_access_token"),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact(Skip = "Requires API configuration files")]
    public async Task ExecuteAsync_ShouldUseCachedToken_WhenTokenExists()
    {
        // Arrange


        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { RT_CD = "0", MSG1 = "Success" }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/hashkey"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new HashkeyResponse { HASH = "test_hash" }))
                };
            }
            return null;
        });

        // Act
        await _client.ExecuteAsync<object>("Order", new { test = "body" });

        // Assert
        // Verify Token Request was NOT made
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP")),
            ItExpr.IsAny<CancellationToken>()
        );

    }

    [Fact(Skip = "Requires API configuration files")]
    public async Task EnsureConnectedAsync_ShouldUseTokenFromRepository_WhenExists()
    {
        // Arrange
        var cachedToken = "repo_cached_token";
        var expiration = DateTime.UtcNow.AddHours(1);

        _mockTokenRepository.Setup(r => r.GetTokenAsync(_accountAlias))
            .ReturnsAsync(new TokenCacheEntry { Token = cachedToken, Expiration = expiration });

        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { RT_CD = "0", MSG1 = "Success" }))
                };
            }
            return null;
        });

        // Act
        await _client.ExecuteAsync<object>("Order", new { test = "body" });

        // Assert
        // Verify Repository was queried
        _mockTokenRepository.Verify(r => r.GetTokenAsync(_accountAlias), Times.Once);

        // Verify Token Request was NEVER made
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/oauth2/tokenP")),
            ItExpr.IsAny<CancellationToken>()
        );

        // Verify Order Request has stored cached token
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash") &&
                req.Headers.Authorization!.Parameter == cachedToken),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact(Skip = "Requires API configuration files")]
    public async Task ExecuteAsync_ShouldGenerateHashkey_ForPostRequests()
    {
        // Arrange


        SetupMockResponse(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/hashkey"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new HashkeyResponse { HASH = "generated_hash_key" }))
                };
            }
            if (req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { RT_CD = "0", MSG1 = "Success" }))
                };
            }
            return null;
        });

        // Act
        await _client.ExecuteAsync<object>("Order", new { test = "body" });

        // Assert
        // Verify Hashkey Request was made
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath.Contains("/uapi/hashkey")),
            ItExpr.IsAny<CancellationToken>()
        );

        // Verify Order Request has Hashkey header
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.AbsolutePath.Contains("/uapi/domestic-stock/v1/trading/order-cash") &&
                req.Headers.Contains("hashkey") &&
                req.Headers.GetValues("hashkey").First() == "generated_hash_key"),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}
