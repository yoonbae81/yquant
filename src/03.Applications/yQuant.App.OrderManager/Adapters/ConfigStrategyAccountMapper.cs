using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace yQuant.App.OrderManager.Adapters;

public class ConfigStrategyAccountMapper : IStrategyAccountMapper
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, List<string>> _strategyMapping;

    public ConfigStrategyAccountMapper(IConfiguration configuration)
    {
        _configuration = configuration;
        _strategyMapping = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var strategiesSection = _configuration.GetSection("Webhook:StrategyAccountMapping");
        if (strategiesSection.Exists())
        {
            foreach (var child in strategiesSection.GetChildren())
            {
                var accounts = child.Get<List<string>>() ?? new List<string>();
                _strategyMapping[child.Key] = accounts;
            }
        }
    }

    public IEnumerable<string> GetAccountAliasesForStrategy(string strategy)
    {
        if (_strategyMapping.TryGetValue(strategy, out var accounts))
        {
            return accounts;
        }

        // Fallback to default strategy "*"
        if (_strategyMapping.TryGetValue("*", out var defaultAccounts))
        {
            return defaultAccounts;
        }

        return Enumerable.Empty<string>();
    }
}
