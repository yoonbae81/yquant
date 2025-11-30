using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands
{
    public class OrderCommand : ICommand
    {
        private readonly KISBrokerAdapter _adapter;
        private readonly ILogger<OrderCommand> _logger;
        private readonly string _accountAlias;
        private readonly string _accountNumber;
        private readonly OrderAction _action;

        public OrderCommand(KISBrokerAdapter adapter, ILogger<OrderCommand> logger, string accountAlias, string accountNumber, OrderAction action)
        {
            _adapter = adapter;
            _logger = logger;
            _accountAlias = accountAlias;
            _accountNumber = accountNumber;
            _action = action;
        }

        public string Name => _action == OrderAction.Buy ? "buy" : "sell";
        public string Description => $"{_action} stock (usage: {Name} <ticker> <qty> [price])";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length < 4)
            {
                System.Console.WriteLine($"Usage: {Name} <ticker> <qty> [price]");
                return;
            }

            var ticker = args[2];
            if (!decimal.TryParse(args[3], out var qty))
            {
                System.Console.WriteLine("Invalid quantity.");
                return;
            }

            decimal? price = null;
            var orderType = OrderType.Market;

            if (args.Length > 4)
            {
                if (!decimal.TryParse(args[4], out var p))
                {
                    System.Console.WriteLine("Invalid price.");
                    return;
                }
                price = p;
                orderType = OrderType.Limit;
            }

            var order = new Order
            {
                AccountAlias = _accountAlias,
                Ticker = ticker,
                Action = _action,
                Type = orderType,
                Qty = qty,
                Price = price,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Placing {Type} {Action} order for {Ticker}: {Qty} @ {Price}", order.Type, order.Action, ticker, qty, price?.ToString() ?? "Market");
            var result = await _adapter.PlaceOrderAsync(order, _accountNumber);
            System.Console.WriteLine($"Order Result: {(result.IsSuccess ? "Success" : "Failed")}");
            System.Console.WriteLine($"Message: {result.Message}");
            if (!string.IsNullOrEmpty(result.BrokerOrderId))
            {
                System.Console.WriteLine($"Broker Order ID: {result.BrokerOrderId}");
            }
        }
    }
}
