using Microsoft.Extensions.Configuration;
using yQuant.Core.Ports.Output.Policies;
using System.Collections.Generic;
using System;

namespace yQuant.App.OrderManager.Adapters;

public class ConfigStrategyPolicyMapper : IStrategyPolicyMapper
{
    private readonly Dictionary<string, string> _mapping;

    public ConfigStrategyPolicyMapper(IConfiguration configuration, IEnumerable<IPositionSizer> availableSizers)
    {
        _mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = configuration.GetSection("OrderManager:StrategySizingMapping");

        var policyNamesToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                if (child.Value != null)
                {
                    _mapping[child.Key] = child.Value;
                    policyNamesToCheck.Add(child.Value);
                }
            }
        }

        // Always check "Basic" as it's the default fallback
        policyNamesToCheck.Add("Basic");

        // Validate that all configured policies have a corresponding implementation
        var sizerTypeNames = availableSizers.Select(s => s.GetType().Name).ToList();

        foreach (var policyName in policyNamesToCheck)
        {
            var exists = sizerTypeNames.Any(name => name.StartsWith(policyName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                throw new InvalidOperationException(
                    $"Configuration Error: Sizing policy '{policyName}' is specified in StrategySizingMapping but no corresponding class starting with '{policyName}' was found in the application. " +
                    $"Available sizers: {string.Join(", ", sizerTypeNames)}");
            }
        }
    }

    public string GetSizingPolicyName(string strategy)
    {
        if (_mapping.TryGetValue(strategy, out var policyName))
        {
            return policyName;
        }

        if (_mapping.TryGetValue("*", out var defaultPolicy))
        {
            return defaultPolicy;
        }

        return "Basic"; // Fallback to Basic
    }
}
