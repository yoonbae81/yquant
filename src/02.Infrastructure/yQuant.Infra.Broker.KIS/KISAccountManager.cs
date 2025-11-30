using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace yQuant.Infra.Broker.KIS;

/// <summary>
/// Manages multiple KIS accounts, each with independent credentials and tokens
/// </summary>
public class KISAccountManager
{
    private readonly Dictionary<string, IKISClient> _clients = new();
    private readonly Dictionary<string, KISBrokerAdapter> _adapters = new();
    private readonly Dictionary<string, Account> _accounts = new();
    private readonly ILogger<KISAccountManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly KISApiConfig _apiConfig;
    private readonly KISAccountProvider _accountProvider;

    public KISAccountManager(
        ILogger<KISAccountManager> logger,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        KISApiConfig apiConfig,
        KISAccountProvider accountProvider)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _apiConfig = apiConfig;
        _accountProvider = accountProvider;

        LoadAccounts();
    }

    private void LoadAccounts()
    {
        var accounts = _accountProvider.GetAccounts();
        foreach (var account in accounts)
        {
            try
            {
                RegisterAccount(account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register KIS account: {Alias}", account.Alias);
            }
        }
    }

    /// <summary>
    /// Register a KIS account
    /// </summary>
    /// <param name="account">The account object to register</param>
    private void RegisterAccount(Account account)
    {
        var httpClient = _httpClientFactory.CreateClient("KIS");
        var kisLogger = _serviceProvider.GetRequiredService<ILogger<KISClient>>();
        
        var kisClient = new KISClient(httpClient, kisLogger, account, _apiConfig);
        
        var adapterLogger = _serviceProvider.GetRequiredService<ILogger<KISBrokerAdapter>>();
        var adapter = new KISBrokerAdapter(kisClient, adapterLogger);
        
        _clients[account.Alias] = kisClient;
        _adapters[account.Alias] = adapter;
        _accounts[account.Alias] = account;
        
        _logger.LogInformation("Registered KIS account: {Alias}", account.Alias);
    }

    /// <summary>
    /// Get the broker adapter for a specific account
    /// </summary>
    public IBrokerAdapter? GetAdapter(string accountAlias)
    {
        return _adapters.GetValueOrDefault(accountAlias);
    }

    /// <summary>
    /// Get the account information for a specific account
    /// </summary>
    public Account? GetAccount(string accountAlias)
    {
        return _accounts.GetValueOrDefault(accountAlias);
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
}
