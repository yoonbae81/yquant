using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IStrategyAccountMapper
{
    /// <summary>
    /// Resolves which account(s) should be used for a given strategy.
    /// </summary>
    /// <param name="strategy">The strategy name from the signal.</param>
    /// <returns>A collection of account aliases.</returns>
    IEnumerable<string> GetAccountAliasesForStrategy(string strategy);
}
