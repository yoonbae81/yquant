using System;
using System.Threading.Tasks;
using yQuant.Infra.Redis.Adapters;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Console.Commands
{
    public class DepositCommand : ICommand
    {
        private readonly RedisBrokerClient _client;

        public DepositCommand(RedisBrokerClient client)
        {
            _client = client;
        }

        public string Name => "deposit";
        public string Description => "Show deposits for specific currency. Usage: deposit <currency> [-r]";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length < 3)
            {
                System.Console.WriteLine("Usage: deposit <currency> [-r]");
                return;
            }

            if (!Enum.TryParse<CurrencyType>(args[2], true, out var currency))
            {
                System.Console.WriteLine($"Invalid currency: {args[2]}. Available: KRW, USD, CNY, JPY, HKD, VND");
                return;
            }

            bool forceRefresh = args.Length > 3 && args[3] == "-r";

            try
            {
                var account = await _client.GetDepositAsync(currency, forceRefresh);
                if (account != null && account.Deposits.TryGetValue(currency, out var amount))
                {
                    System.Console.WriteLine($"[{currency}] Deposit: {amount:N2}");
                }
                else
                {
                    System.Console.WriteLine($"[{currency}] Deposit: 0");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
