using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;

namespace yQuant.Infra.Broker.KIS;

public class KISAccountProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KISAccountProvider> _logger;

    public KISAccountProvider(IConfiguration configuration, ILogger<KISAccountProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Account? GetAccount(string alias)
    {
        var accountsSection = _configuration.GetSection("Accounts");
        var accountSection = accountsSection.GetChildren()
            .FirstOrDefault(a => a["Alias"]?.Equals(alias, StringComparison.OrdinalIgnoreCase) == true);

        if (accountSection == null)
        {
            _logger.LogWarning("Account '{Alias}' not found in configuration.", alias);
            return null;
        }

        return CreateAccountFromSection(accountSection);
    }

    public IEnumerable<Account> GetAccounts()
    {
        var accountsSection = _configuration.GetSection("Accounts");
        foreach (var accountSection in accountsSection.GetChildren())
        {
            var account = CreateAccountFromSection(accountSection);
            if (account != null)
            {
                yield return account;
            }
        }
    }

    private Account? CreateAccountFromSection(IConfigurationSection section)
    {
        var alias = section["Alias"];
        var broker = section["Broker"];
        
        if (string.IsNullOrEmpty(alias) || !string.Equals(broker, "KIS", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var appKey = section["AppKey"];
        var appSecret = section["AppSecret"];
        var accountNumber = section["Number"];

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(accountNumber))
        {
            _logger.LogWarning("Skipping KIS account {Alias} due to missing credentials.", alias);
            return null;
        }

        return new Account
        {
            Alias = alias!,
            Number = accountNumber!,
            Broker = "KIS",
            AppKey = appKey!,
            AppSecret = appSecret!,
            Deposits = new Dictionary<CurrencyType, decimal>(),
            Active = true
        };
    }
}
