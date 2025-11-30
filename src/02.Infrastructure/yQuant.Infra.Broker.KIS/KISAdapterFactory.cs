using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Broker.KIS;

public class KISAdapterFactory : IBrokerAdapterFactory
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KISApiConfig _apiConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<KISAdapterFactory> _logger;

    // Cache clients/adapters to avoid recreating them (and to share state like tokens)
    private readonly Dictionary<string, KISClient> _clients = new();
    private readonly Dictionary<string, IBrokerAdapter> _adapters = new();
    private readonly object _lock = new();

    public KISAdapterFactory(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<KISAdapterFactory>();
        
        // Load API Config internally
        var apiPath = Path.Combine(AppContext.BaseDirectory, "API");
        _apiConfig = KISApiConfig.Load(apiPath);
        System.Console.WriteLine("DEBUG: KISAdapterFactory initialized.");
    }

    public IEnumerable<string> GetAvailableAccounts()
    {
        var accountsSection = _configuration.GetSection("Accounts");
        System.Console.WriteLine($"DEBUG: GetAvailableAccounts found {accountsSection.GetChildren().Count()} children.");
        foreach (var section in accountsSection.GetChildren())
        {
            var alias = section["Alias"];
            var broker = section["Broker"];
            System.Console.WriteLine($"DEBUG: Found account Alias='{alias}', Broker='{broker}'");
            if (!string.IsNullOrEmpty(alias) && string.Equals(broker, "KIS", StringComparison.OrdinalIgnoreCase))
            {
                yield return alias;
            }
        }
    }

    public IBrokerAdapter? GetAdapter(string alias)
    {
        lock (_lock)
        {
            if (_adapters.TryGetValue(alias, out var adapter))
            {
                return adapter;
            }

            var account = CreateAccountFromConfig(alias);
            if (account == null)
            {
                return null;
            }

            var client = GetOrCreateClient(account);
            var adapterLogger = _loggerFactory.CreateLogger<KISBrokerAdapter>();
            adapter = new KISBrokerAdapter(client, adapterLogger);
            
            _adapters[alias] = adapter;
            return adapter;
        }
    }

    public Account? GetAccount(string alias)
    {
        return CreateAccountFromConfig(alias);
    }

    public async Task InvalidateTokenAsync(string alias)
    {
        KISClient? client;
        lock (_lock)
        {
            // Ensure client exists
            if (!_clients.TryGetValue(alias, out client))
            {
                var account = CreateAccountFromConfig(alias);
                if (account != null)
                {
                    client = GetOrCreateClient(account);
                }
            }
        }

        if (client != null)
        {
            await client.InvalidateTokenAsync();
        }
    }

    public async Task EnsureConnectedAsync(string alias)
    {
        KISClient? client;
        lock (_lock)
        {
             if (!_clients.TryGetValue(alias, out client))
            {
                var account = CreateAccountFromConfig(alias);
                if (account != null)
                {
                    client = GetOrCreateClient(account);
                }
            }
        }

        if (client != null)
        {
            await client.EnsureConnectedAsync();
        }
    }

    private KISClient GetOrCreateClient(Account account)
    {
        if (_clients.TryGetValue(account.Alias, out var client))
        {
            return client;
        }

        var clientLogger = _loggerFactory.CreateLogger<KISClient>();
        var httpClient = _httpClientFactory.CreateClient("KIS");
        client = new KISClient(httpClient, clientLogger, account, _apiConfig);
        
        _clients[account.Alias] = client;
        return client;
    }

    private Account? CreateAccountFromConfig(string alias)
    {
        var accountsSection = _configuration.GetSection("Accounts");
        System.Console.WriteLine($"DEBUG: CreateAccountFromConfig searching for '{alias}' in {accountsSection.GetChildren().Count()} accounts.");
        
        var accountSection = accountsSection.GetChildren()
            .FirstOrDefault(a => a["Alias"]?.Equals(alias, StringComparison.OrdinalIgnoreCase) == true);

        if (accountSection == null)
        {
            _logger.LogWarning("Account '{Alias}' not found in configuration.", alias);
            return null;
        }

        var broker = accountSection["Broker"];
        if (!string.Equals(broker, "KIS", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var appKey = accountSection["AppKey"];
        var appSecret = accountSection["AppSecret"];
        var number = accountSection["Number"];

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(number))
        {
            _logger.LogWarning("Skipping KIS account {Alias} due to missing credentials.", alias);
            return null;
        }

        return new Account
        {
            Alias = alias,
            Number = number!,
            Broker = "KIS",
            AppKey = appKey,
            AppSecret = appSecret,
            Deposits = new Dictionary<CurrencyType, decimal>(),
            Active = true
        };
    }
}
