using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IAccountRegistry
{
    /// <summary>
    /// Resolves the Account ID (Alias) to use for a specific target currency.
    /// </summary>
    /// <param name="currency">The target currency.</param>
    /// <returns>The account ID/Alias, or null if no mapping exists.</returns>
    string? GetAccountAliasForCurrency(CurrencyType currency);
}
