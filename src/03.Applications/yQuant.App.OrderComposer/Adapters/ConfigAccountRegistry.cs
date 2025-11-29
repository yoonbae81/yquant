using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.OrderComposer.Adapters;

public class ConfigAccountRegistry : IAccountRegistry
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string> _accountMapping;

    public ConfigAccountRegistry(IConfiguration configuration)
    {
        _configuration = configuration;
        _accountMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        var mappingSection = _configuration.GetSection("AccountMapping");
        if (mappingSection.Exists())
        {
            foreach (var child in mappingSection.GetChildren())
            {
                _accountMapping[child.Key] = child.Value ?? string.Empty;
            }
        }
    }

    public string? GetAccountAliasForCurrency(CurrencyType currency)
    {
        return _accountMapping.GetValueOrDefault(currency.ToString());
    }
}
