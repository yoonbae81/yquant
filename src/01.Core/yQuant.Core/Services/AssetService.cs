using System.Threading.Tasks;
using System.Collections.Generic;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Core.Services;

public class AssetService
{
    private readonly IBrokerAdapterFactory _adapterFactory;

    public AssetService(IBrokerAdapterFactory adapterFactory)
    {
        _adapterFactory = adapterFactory;
    }

    public async Task<Account?> GetAccountOverviewAsync(string accountAlias)
    {
        var adapter = _adapterFactory.GetAdapter(accountAlias);
        if (adapter == null)
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
