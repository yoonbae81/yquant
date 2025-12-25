using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Policies;
using Microsoft.Extensions.Logging;

namespace yQuant.Policies.Sizing;

/// <summary>
/// Sizing policy that always orders exactly one unit if funds are available.
/// Useful for testing strategies or small-scale trading.
/// </summary>
public class OnlyOnePositionSizer : IPositionSizer
{
    private readonly ILogger<OnlyOnePositionSizer> _logger;

    public OnlyOnePositionSizer(ILogger<OnlyOnePositionSizer> logger)
    {
        _logger = logger;
    }

    public Order? CalculatePositionSize(Signal signal, Account account)
    {
        decimal currentPrice = signal.Price ?? 0m;
        if (currentPrice <= 0)
        {
            _logger.LogWarning("Invalid current price ({Price}) for signal {SignalId}. Price must be greater than zero.", currentPrice, signal.Id);
            return null;
        }

        CurrencyType currency = signal.Currency ?? CurrencyType.KRW;
        decimal cash = account.Deposits.GetValueOrDefault(currency, 0);

        if (signal.Action == OrderAction.Buy)
        {
            if (cash < currentPrice)
            {
                _logger.LogInformation("Account {AccountAlias} has insufficient cash ({Cash} {Currency}) to buy 1 unit of {Ticker} at {Price}.",
                    account.Alias, cash, currency, signal.Ticker, currentPrice);
                return null;
            }

            return CreateOrder(signal, account, 1, currentPrice);
        }
        else if (signal.Action == OrderAction.Sell)
        {
            // For OnlyOne policy, we assume we want to sell the 1 unit we held.
            // Broker-side validation will handle if we don't actually hold the position.
            return CreateOrder(signal, account, 1, currentPrice);
        }

        return null;
    }

    public bool ValidateOrder(Order order, Account account, out string failureReason)
    {
        if (order.Qty <= 0)
        {
            failureReason = "Order quantity must be positive (1 for OnlyOne policy).";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private Order CreateOrder(Signal signal, Account account, int qty, decimal price)
    {
        return new Order
        {
            AccountAlias = account.Alias,
            Ticker = signal.Ticker,
            Action = signal.Action,
            Type = OrderType.Market,
            Qty = qty,
            Price = price,
            Timestamp = DateTime.UtcNow
        };
    }
}
