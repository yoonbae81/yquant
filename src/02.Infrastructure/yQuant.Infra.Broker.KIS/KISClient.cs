using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS.Models;
using yQuant.Infra.Broker.KIS.Models;

namespace yQuant.Infra.Broker.KIS;

public class KISClient : IKISClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KISClient> _logger;
    private readonly Account _account;
    private readonly KISApiConfig _apiConfig;
    private readonly RateLimiter _rateLimiter;

    private const string BaseUrl = "https://openapi.koreainvestment.com:9443";

    private string? _accessToken;
    private DateTime _accessTokenExpiration = DateTime.MinValue;

    public Account Account => _account;

    public KISClient(HttpClient httpClient, ILogger<KISClient> logger, Account account, KISApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _logger = logger;
        _account = account;
        _apiConfig = apiConfig;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _logger.LogInformation("KISClient initialized with BaseUrl: {BaseUrl}", BaseUrl);

        _rateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 100
        });
    }

    public async Task EnsureConnectedAsync()
    {
        // 1. Check memory cache first
        if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiration > DateTime.UtcNow.AddMinutes(1))
        {
            return;
        }


        // 3. Check local file cache (fallback when Redis is unavailable)
        var tokenFromFile = await LoadTokenFromFileAsync();
        if (tokenFromFile != null && tokenFromFile.Expiration > DateTime.UtcNow.AddMinutes(1))
        {
            _accessToken = tokenFromFile.Token;
            _accessTokenExpiration = tokenFromFile.Expiration;
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            _logger.LogInformation("Loaded KIS access token from local file cache for account {Alias}.", _account.Alias);
            return;
        }

        // 4. Fetch from API
        await GetAccessTokenAsync();
    }

    private async Task<TokenCacheEntry?> LoadTokenFromFileAsync()
    {
        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yQuant", "KIS", "tokens");
            var cacheFile = Path.Combine(cacheDir, $"{_account.Alias}.json");

            if (!File.Exists(cacheFile))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(cacheFile);
            return JsonSerializer.Deserialize<TokenCacheEntry>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load token from local file cache.");
            return null;
        }
    }

    private async Task SaveTokenToFileAsync(string token, DateTime expiration)
    {
        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yQuant", "KIS", "tokens");
            Directory.CreateDirectory(cacheDir);

            var cacheFile = Path.Combine(cacheDir, $"{_account.Alias}.json");
            var entry = new TokenCacheEntry { Token = token, Expiration = expiration };
            var json = JsonSerializer.Serialize(entry);

            await File.WriteAllTextAsync(cacheFile, json);
            _logger.LogInformation("Saved KIS access token to local file cache for account {Alias}.", _account.Alias);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save token to local file cache.");
        }
    }

    public async Task InvalidateTokenAsync()
    {
        _accessToken = null;
        _accessTokenExpiration = DateTime.MinValue;


        // Clear from local file
        try
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yQuant", "KIS", "tokens");
            var cacheFile = Path.Combine(cacheDir, $"{_account.Alias}.json");
            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
                _logger.LogInformation("Deleted KIS access token from local file cache for account {Alias}.", _account.Alias);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete token from local file cache.");
        }

        _logger.LogInformation("Invalidated KIS access token for account {Alias}.", _account.Alias);
    }

    private async Task GetAccessTokenAsync()
    {
        if (!_apiConfig.TryGetValue("Token", out var endpoint))
        {
            throw new InvalidOperationException("Token endpoint not defined in API spec.");
        }

        _logger.LogInformation("Attempting to get new KIS access token for account {Alias}...", _account.Alias);

        var requestBody = new
        {
            grant_type = "client_credentials",
            appkey = _account.AppKey,
            appsecret = _account.AppSecret
        };

        using var lease = await _rateLimiter.AcquireAsync();
        if (!lease.IsAcquired)
        {
            throw new RateLimitExceededException("KIS access token request rate limit exceeded.");
        }

        var response = await _httpClient.PostAsJsonAsync(endpoint!.Path, requestBody);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            _accessToken = tokenResponse.AccessToken;
            _accessTokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
            

            // Store in local file cache
            await SaveTokenToFileAsync(_accessToken, _accessTokenExpiration);

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            _logger.LogInformation("Successfully obtained KIS access token for account {Alias}. Expires in {Seconds}s.", _account.Alias, tokenResponse.ExpiresIn);
        }
        else
        {
            throw new InvalidOperationException("Failed to obtain KIS access token.");
        }
    }

    private async Task<string> GetHashkeyAsync(object body)
    {
        if (!_apiConfig.TryGetValue("Hashkey", out var endpoint))
        {
            throw new InvalidOperationException("Hashkey endpoint not defined in API spec.");
        }

        var requestBody = new
        {
            appkey = _account.AppKey,
            appsecret = _account.AppSecret,
            JsonBody = body
        };

        // Hashkey request does not need Authorization header, but needs appkey/appsecret in body (or header? Spec says body for some, header for others. Markdown says body is encrypted? No, markdown says send body to hashkey endpoint)
        // Markdown: "Send the JSON body of the order request to this endpoint... returns hash value"
        // Spec in kis-api-spec.json says: Parameters: appkey, appsecret. 
        // Wait, the markdown says "Request Body: JsonBody (The body to be sent via POST)".
        // And also headers: appkey, appsecret, content-type.
        
        // Let's follow the markdown carefully:
        // Request Header: content-type, appkey, appsecret
        // Request Body: JsonBody (The actual body of the order)
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint!.Path);
        requestMessage.Headers.Add("appkey", _account.AppKey);
        requestMessage.Headers.Add("appsecret", _account.AppSecret);
        requestMessage.Content = JsonContent.Create(body); // The body of the order is sent as the content

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var hashResponse = await response.Content.ReadFromJsonAsync<HashkeyResponse>();
        return hashResponse?.HASH ?? throw new InvalidOperationException("Failed to retrieve Hashkey.");
    }

    public async Task<TResponse?> ExecuteAsync<TResponse>(string endpointName, object? body = null, Dictionary<string, string>? queryParams = null, Dictionary<string, string>? headers = null, string? trIdVariant = null)
    {
        await EnsureConnectedAsync();

        if (!_apiConfig.TryGetValue(endpointName, out var endpoint))
        {
            throw new InvalidOperationException($"Endpoint '{endpointName}' not defined in API spec.");
        }

        using var lease = await _rateLimiter.AcquireAsync();
        if (!lease.IsAcquired)
        {
            throw new RateLimitExceededException($"Rate limit exceeded for {endpointName}.");
        }

        var requestMessage = new HttpRequestMessage(new HttpMethod(endpoint!.Method), BuildUrl(endpoint.Path, queryParams));

        string? trId = endpoint.TrId;
        if (!string.IsNullOrEmpty(trIdVariant) && endpoint.TrIdMap != null && endpoint.TrIdMap.TryGetValue(trIdVariant, out var mappedTrId))
        {
            trId = mappedTrId;
        }

        if (trId != null)
        {
            requestMessage.Headers.Add("tr_id", trId);
        }
        requestMessage.Headers.Add("appkey", _account.AppKey);
        requestMessage.Headers.Add("appsecret", _account.AppSecret);
        requestMessage.Headers.Add("Accept", "application/json");

        if (headers != null)
        {
            foreach (var header in headers)
            {
                requestMessage.Headers.Add(header.Key, header.Value);
            }
        }

        if (body != null && (endpoint.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) || endpoint.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
        {
            // For POST requests (excluding Token and Hashkey themselves), generate and add Hashkey
            if (endpointName != "Token" && endpointName != "Hashkey")
            {
                try 
                {
                    var hashkey = await GetHashkeyAsync(body);
                    requestMessage.Headers.Add("hashkey", hashkey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate hashkey. Proceeding without it (might fail if required).");
                }
            }
            
            requestMessage.Content = JsonContent.Create(body);
        }
        else if (endpoint.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            // KIS API might require Content-Type even for GET requests
            requestMessage.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(requestMessage);
        
        // Log error details if request failed
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("KIS API Error - Endpoint: {Endpoint}, Status: {Status}, Response: {Response}", 
                endpointName, response.StatusCode, errorContent);
        }
        
        response.EnsureSuccessStatusCode();

        // Handle string response for PlaceOrderAsync which returns string in original code
        if (typeof(TResponse) == typeof(string))
        {
            var content = await response.Content.ReadAsStringAsync();
            return (TResponse)(object)content;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    private string BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{path}?{queryString}";
    }
}
