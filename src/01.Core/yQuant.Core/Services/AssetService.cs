using System.Threading.Tasks;
using System.Collections.Generic;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Core.Services;

public class AssetService
{
    private readonly Dictionary<string, IBrokerAdapter> _adapters;

    public AssetService(Dictionary<string, IBrokerAdapter> adapters)
    {
        _adapters = adapters;
    }

    public async Task<Account?> GetAccountOverviewAsync(string accountAlias)
    {
        if (!_adapters.TryGetValue(accountAlias, out var adapter))
        {
            return null;
        }

        // 1. Get Account State (Deposits)
        var account = await adapter.GetAccountStateAsync();

        // 2. Get Positions
        var positions = await adapter.GetPositionsAsync();
        
        // 3. Merge Positions into Account
        account.Positions = positions;
        account.Alias = accountAlias; // Ensure alias is set

        return account;
    }
}
