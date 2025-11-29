using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IAccountRepository
{
    /// <summary>
    /// Retrieves the current state of an account by its ID (Alias).
    /// </summary>
    /// <param name="accountAlias">The account alias/ID.</param>
    /// <returns>The account state, or null if not found.</returns>
    Task<Account?> GetAccountAsync(string accountAlias);
}
