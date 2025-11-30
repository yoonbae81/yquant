using System;
using System.Threading.Tasks;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands
{
    public class InfoCommand : ICommand
    {
        private readonly KISBrokerAdapter _adapter;

        public InfoCommand(KISBrokerAdapter adapter)
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
            var priceInfo = await _adapter.GetPriceAsync(ticker);
            System.Console.WriteLine($"Ticker: {ticker}");
            System.Console.WriteLine($"Price: {priceInfo.CurrentPrice:N2}");
            System.Console.WriteLine($"Change: {priceInfo.ChangeRate:N2}%");
        }
    }
}
