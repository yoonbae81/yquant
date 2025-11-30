using System;
using System.Threading.Tasks;
using yQuant.Core.Models;
using yQuant.Core.Services;

namespace yQuant.App.Console.Commands
{
    public class PositionsCommand : ICommand
    {
        private readonly AssetService _assetService;
        private readonly string _accountNumber;

        public PositionsCommand(AssetService assetService, string accountNumber)
        {
            _assetService = assetService;
            _accountNumber = accountNumber;
        }

        public string Name => "positions";
        public string Description => "Show account positions";
        public async Task ExecuteAsync(string[] args)
        {
            var account = await _assetService.GetAccountOverviewAsync();
            
            System.Console.WriteLine("========================================");
            System.Console.WriteLine($"Account Summary: {account.Alias} ({account.Number})");
            System.Console.WriteLine("========================================");
            
            System.Console.WriteLine($"\nTotal Equity (KRW): {account.GetTotalEquity(CurrencyType.KRW):N0} KRW");
            System.Console.WriteLine($"Total Equity (USD): {account.GetTotalEquity(CurrencyType.USD):N2} USD");

            System.Console.WriteLine("\n[Positions]");
            System.Console.WriteLine($"{"Ticker",-10} {"Qty",-10} {"AvgPrice",-15} {"CurPrice",-15} {"PnL",-15} {"Return%",-10}");
            System.Console.WriteLine(new string('-', 80));

            foreach (var pos in account.Positions)
            {
                var pnl = pos.UnrealizedPnL;
                var returnRate = pos.AvgPrice != 0 ? (pos.CurrentPrice - pos.AvgPrice) / pos.AvgPrice * 100 : 0;
                
                System.Console.WriteLine($"{pos.Ticker,-10} {pos.Qty,-10} {pos.AvgPrice,-15:N2} {pos.CurrentPrice,-15:N2} {pnl,-15:N2} {returnRate,-10:F2}%");
            }
            System.Console.WriteLine("========================================");
        }
    }
}
