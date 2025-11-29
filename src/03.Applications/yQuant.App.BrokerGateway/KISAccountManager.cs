using Microsoft.Extensions.Logging;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Middleware.Redis.Interfaces;

namespace yQuant.App.BrokerGateway;

/// <summary>
/// Manages multiple KIS accounts, each with independent credentials and tokens
/// </summary>
public class KISAccountManager
{
    private readonly Dictionary<string, KisConnector> _clients = new();
    private readonly Dictionary<string, KisBrokerAdapter> _adapters = new();
    private readonly ILogger<KISAccountManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRedisService _redisService;

    public KISAccountManager(
        ILogger<KISAccountManager> logger,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        IRedisService redisService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _redisService = redisService;
    }

    /// <summary>
    /// Register a KIS account with its credentials
    /// </summary>
    /// <param name="accountAlias">Internal alias for the account</param>
    /// <param name="userId">KIS User ID for token requests</param>
    public void RegisterAccount(string accountAlias, string userId, string appKey, string appSecret, string baseUrl, string accountNumber)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("KIS");
            var kisLogger = _serviceProvider.GetRequiredService<ILogger<KisConnector>>();
            
            var kisClient = new KisConnector(httpClient, kisLogger, _redisService, userId, accountAlias, appKey, appSecret, baseUrl);
            
            var adapterLogger = _serviceProvider.GetRequiredService<ILogger<KisBrokerAdapter>>();
            var accountPrefix = ExtractAccountPrefix(accountNumber);
            var adapter = new KisBrokerAdapter(adapterLogger, kisClient, accountPrefix, userId, accountAlias);
            
            _clients[accountAlias] = kisClient;
            _adapters[accountAlias] = adapter;
            
            _logger.LogInformation("Registered KIS account: {Alias} (User: {UserId})", accountAlias, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register KIS account: {Alias}", accountAlias);
            throw;
        }
    }

    /// <summary>
    /// Get the broker adapter for a specific account
    /// </summary>
    public IBrokerAdapter? GetAdapter(string accountAlias)
    {
        return _adapters.GetValueOrDefault(accountAlias);
    }

    /// <summary>
    /// Check if an account is registered
    /// </summary>
    public bool HasAccount(string accountAlias)
    {
        return _adapters.ContainsKey(accountAlias);
    }

    /// <summary>
    /// Get all registered account IDs
    /// </summary>
    public IEnumerable<string> GetAccountAliases()
    {
        return _adapters.Keys;
    }

    /// <summary>
    /// Extract account prefix from full account number (e.g., "12345678-01" -> "12345678")
    /// </summary>
    private static string ExtractAccountPrefix(string accountNumber)
    {
        var parts = accountNumber.Split('-');
        return parts.Length > 0 ? parts[0] : accountNumber;
    }
}
