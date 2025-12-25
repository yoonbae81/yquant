using System;
using System.Threading.Tasks;
using yQuant.Infra.Redis.Adapters;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Console.Commands
{
    public class PositionsCommand : ICommand
    {
        private readonly RedisBrokerClient _client;

        public PositionsCommand(RedisBrokerClient client)
        {
            _client = client;
        }

        public string Name => "positions";
        public string Description => "Show positions for specific country. Usage: positions <country> [-r]";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length < 3)
            {
                System.Console.WriteLine("Usage: positions <country> [-r]");
                return;
            }

            if (!Enum.TryParse<CountryCode>(args[2], true, out var country))
            {
                System.Console.WriteLine($"Invalid country: {args[2]}. Available: KR, US, VN, HK, CN, JP");
                return;
            }

            bool forceRefresh = args.Length > 3 && args[3] == "-r";

            try
            {
                var positions = await _client.GetPositionsAsync(country, forceRefresh);
                
                System.Console.WriteLine($"\n[Positions - {country}]");
                System.Console.WriteLine($"{"Ticker",-10} {"Qty",-10} {"AvgPrice",-15} {"CurPrice",-15} {"PnL",-15} {"Return%",-10}");
                System.Console.WriteLine(new string('-', 80));

                if (positions != null)
                {
                    foreach (var pos in positions)
                    {
                        var pnl = pos.UnrealizedPnL;
                        var returnRate = pos.AvgPrice != 0 ? (pos.CurrentPrice - pos.AvgPrice) / pos.AvgPrice * 100 : 0;
                        
                        System.Console.WriteLine($"{pos.Ticker,-10} {pos.Qty,-10} {pos.AvgPrice,-15:N2} {pos.CurrentPrice,-15:N2} {pnl,-15:N2} {returnRate,-10:F2}%");
                    }
                }
                System.Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
