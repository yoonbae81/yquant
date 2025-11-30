using System;
using System.Threading.Tasks;
using yQuant.Core.Services;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands
{
    public class DepositCommand : ICommand
    {
        private readonly AssetService _assetService;
        private readonly string _accountNumber;

        public DepositCommand(AssetService assetService, string accountNumber)
        {
            _assetService = assetService;
            _accountNumber = accountNumber;
        }

        public string Name => "deposit";
        public string Description => "Show deposits for KRW, USD, VND, etc.";

        public async Task ExecuteAsync(string[] args)
        {
            var account = await _assetService.GetAccountOverviewAsync();

            System.Console.WriteLine("\n[Deposits]");
            foreach (var deposit in account.Deposits)
            {
                System.Console.WriteLine($"- {deposit.Key}: {deposit.Value:N2}");
            }
        }
    }
}
