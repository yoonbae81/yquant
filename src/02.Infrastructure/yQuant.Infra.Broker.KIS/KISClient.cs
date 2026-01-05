using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS.Models;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Notification;

namespace yQuant.Infra.Broker.KIS;

public class KISClient : IKISClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KISClient> _logger;
    private readonly Account _account;
    private readonly KISApiConfig _apiConfig;
    private readonly RateLimiter _rateLimiter;
    private readonly IStorageValkeyService? _storageValkey;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private string? _accessToken;
    private DateTime _accessTokenExpiration = DateTime.MinValue;
    private static bool _isBaseUrlLogged = false;

    // Global Rate Limiter shared across ALL KISClient instances
    // KIS API Limit: 20 req/sec (Real Trading)
    // This ensures total API calls across all accounts never exceed KIS server limit
    private static readonly RateLimiter _globalRateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 20,  // Match KIS API limit
        Window = TimeSpan.FromSeconds(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 200  // Handle bursts
    });

    public Account Account => _account;

    public KISClient(HttpClient httpClient, ILogger<KISClient> logger, Account account, KISApiConfig apiConfig, string baseUrl, IStorageValkeyService? storageValkey = null, int rateLimit = 20)
    {
        _httpClient = httpClient;
        _logger = logger;
        _account = account;
        _apiConfig = apiConfig;
        _storageValkey = storageValkey;

        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!_isBaseUrlLogged)
        {
            _logger.LogInformation("KISClient initialized with BaseUrl: {BaseUrl}, Global Rate Limit: 20 req/sec", baseUrl);
            _isBaseUrlLogged = true;
        }

        // Per-account rate limiter (secondary protection)
        // This prevents a single account from monopolizing the global limit
        _rateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Min(rateLimit, 20),  // Cap at 20 to match global limit
            Window = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 50
        });
    }

    public async Task EnsureConnectedAsync()
    {
        // 1. Check memory cache first (Pre-lock check)
        if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiration > DateTime.UtcNow.AddMinutes(1))
        {
            return;
        }

        await _connectionLock.WaitAsync();
        try
        {
            // 2. Check memory cache again (Post-lock check)
            if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiration > DateTime.UtcNow.AddMinutes(1))
            {
                return;
            }

            // 3. Check Global Token Valkey (Shared across environments)
            if (_storageValkey != null)
            {
                var tokenFromValkey = await _storageValkey.GetAsync<TokenCacheEntry>($"Token:KIS:{_account.Alias}");
                if (tokenFromValkey != null && tokenFromValkey.Expiration > DateTime.UtcNow.AddMinutes(1))
                {
                    _accessToken = tokenFromValkey.Token;
                    _accessTokenExpiration = tokenFromValkey.Expiration;
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                    _logger.LogInformation("Loaded KIS access token from GLOBAL Valkey cache for account {Alias}.", _account.Alias);

                    // Also update local file cache as fallback
                    await SaveTokenToFileAsync(_accessToken, _accessTokenExpiration);
                    return;
                }
            }

            // 4. Check local file cache (fallback when Valkey is unavailable)
            var tokenFromFile = await LoadTokenFromFileAsync();
            if (tokenFromFile != null && tokenFromFile.Expiration > DateTime.UtcNow.AddMinutes(1))
            {
                _accessToken = tokenFromFile.Token;
                _accessTokenExpiration = tokenFromFile.Expiration;
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                _logger.LogInformation("Loaded KIS access token from local file cache for account {Alias}.", _account.Alias);
                return;
            }

            // 5. Fetch from API
            await GetAccessTokenAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
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


        // Clear from Global Valkey
        if (_storageValkey != null)
        {
            await _storageValkey.DeleteAsync($"Token:KIS:{_account.Alias}");
        }

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

        // Use global rate limiter for token requests too
        using var globalLease = await _globalRateLimiter.AcquireAsync();
        if (!globalLease.IsAcquired)
        {
            throw new RateLimitExceededException("Global KIS API rate limit exceeded for token request.");
        }

        using var accountLease = await _rateLimiter.AcquireAsync();
        if (!accountLease.IsAcquired)
        {
            throw new RateLimitExceededException("Account rate limit exceeded for token request.");
        }

        var response = await _httpClient.PostAsJsonAsync(endpoint!.Path, requestBody);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            _accessToken = tokenResponse.AccessToken;
            _accessTokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);


            // Store in Global Valkey
            if (_storageValkey != null)
            {
                await _storageValkey.SetAsync($"Token:KIS:{_account.Alias}", new TokenCacheEntry { Token = _accessToken, Expiration = _accessTokenExpiration }, TimeSpan.FromSeconds(tokenResponse.ExpiresIn));
            }

            // Store in local file cache
            await SaveTokenToFileAsync(_accessToken, _accessTokenExpiration);

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            _logger.LogInformation("Successfully obtained KIS access token for account {Alias}. Expires in {Seconds}s.", _account.Alias, tokenResponse.ExpiresIn);

            // Notify via uniform Logger extension
            _logger.LogSecurityNotification($"Token issued for '{_account.Alias}' (Host: {Environment.MachineName})", _account.Alias);
        }
        else
        {
            throw new InvalidOperationException("Failed to obtain KIS access token.");
        }
    }




    public async Task<TResponse?> ExecuteAsync<TResponse>(string endpointName, object? body = null, Dictionary<string, string>? queryParams = null, Dictionary<string, string>? headers = null, string? trIdVariant = null)
    {
        await EnsureConnectedAsync();

        if (!_apiConfig.TryGetValue(endpointName, out var endpoint))
        {
            throw new InvalidOperationException($"Endpoint '{endpointName}' not defined in API spec.");
        }

        // Dual-layer rate limiting:
        // 1. Global rate limiter (shared across ALL accounts) - enforces KIS API server limit
        // 2. Per-account rate limiter - prevents single account monopolization

        using var globalLease = await _globalRateLimiter.AcquireAsync();
        if (!globalLease.IsAcquired)
        {
            _logger.LogWarning("Global KIS API rate limit exceeded for {Endpoint}. Request queued.", endpointName);
            throw new RateLimitExceededException($"Global rate limit exceeded for {endpointName}.");
        }

        using var accountLease = await _rateLimiter.AcquireAsync();
        if (!accountLease.IsAcquired)
        {
            _logger.LogWarning("Account {Alias} rate limit exceeded for {Endpoint}. Request queued.", _account.Alias, endpointName);
            throw new RateLimitExceededException($"Account rate limit exceeded for {endpointName}.");
        }

        var requestMessage = new HttpRequestMessage(new HttpMethod(endpoint!.Method), BuildUrl(endpoint.Path, queryParams));

        // Log the API request with endpoint name and key parameters
        var logMessage = $"KIS API Request - Endpoint: {endpointName}";
        if (queryParams != null && queryParams.Count > 0)
        {
            // Extract ticker/symbol if present
            if (queryParams.TryGetValue("symb", out var ticker) ||
                queryParams.TryGetValue("symbol", out ticker) ||
                queryParams.TryGetValue("pdno", out ticker))
            {
                logMessage += $", Ticker: {ticker}";
            }
            // Show all query params for debugging
            var paramStr = string.Join(", ", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
            logMessage += $", Params: [{paramStr}]";
        }
        _logger.LogInformation(logMessage);

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
            // Extract ticker from body if present (for order requests)
            try
            {
                var bodyJson = JsonSerializer.Serialize(body);
                using var doc = JsonDocument.Parse(bodyJson);
                if (doc.RootElement.TryGetProperty("pdno", out var pdno))
                {
                    _logger.LogInformation("KIS API Request Body - Ticker: {Ticker}", pdno.GetString());
                }
                else if (doc.RootElement.TryGetProperty("symb", out var symb))
                {
                    _logger.LogInformation("KIS API Request Body - Ticker: {Ticker}", symb.GetString());
                }
            }
            catch
            {
                // Ignore JSON parsing errors for logging
            }

            // For POST requests (excluding Token and Hashkey themselves), generate and add Hashkey
            if (endpointName != "Token" && endpointName != "Hashkey")
            {
                var jsonBody = JsonSerializer.Serialize(body);
                requestMessage.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            }
            else
            {
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            }
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

        // Log raw JSON for OverseasPrice to debug empty fields
        if (endpointName == "OverseasPrice")
        {
            var rawJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("KIS OverseasPrice raw response: {Json}", rawJson);

            // Re-create content for deserialization
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(rawJson);
            response.Content = new ByteArrayContent(jsonBytes);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        // Handle string response for PlaceOrderAsync which returns string in original code
        if (typeof(TResponse) == typeof(string))
        {
            var content = await response.Content.ReadAsStringAsync();
            return (TResponse)(object)content;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    private static string BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{path}?{queryString}";
    }
}
