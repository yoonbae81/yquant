using System.Threading.Tasks;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Core.Services;

public class AssetService
{
    private readonly IBrokerAdapter _brokerAdapter;

    public AssetService(IBrokerAdapter brokerAdapter)
    {
        _brokerAdapter = brokerAdapter;
    }

    public async Task<Account> GetAccountOverviewAsync(string accountNumber)
    {
        // 1. Get Account State (Deposits)
        var account = await _brokerAdapter.GetAccountStateAsync(accountNumber);

        // 2. Get Positions
        var positions = await _brokerAdapter.GetPositionsAsync(accountNumber);
        
        // 3. Merge Positions into Account
        account.Positions = positions;

        return account;
    }
}
