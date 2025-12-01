using System;
using System.Threading.Tasks;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Console.Commands
{
    public class InfoCommand : ICommand
    {
        private readonly IBrokerAdapter _adapter;

        public InfoCommand(IBrokerAdapter adapter)
        {
            _adapter = adapter;
        }

        public string Name => "info";
        public string Description => "Check price for a ticker (usage: info <ticker>)";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length < 3)
            {
                System.Console.WriteLine("Usage: info <ticker>");
                return;
            }

            var ticker = args[2];
            try 
            {
                var priceInfo = await _adapter.GetPriceAsync(ticker);
                if (priceInfo != null)
                {
                    System.Console.WriteLine($"Ticker: {ticker}");
                    System.Console.WriteLine($"Price: {priceInfo.CurrentPrice:N2}");
                    System.Console.WriteLine($"Change: {priceInfo.ChangeRate:N2}%");
                }
                else
                {
                    System.Console.WriteLine($"Failed to get price for {ticker}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
