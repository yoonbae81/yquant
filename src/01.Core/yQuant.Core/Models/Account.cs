using System.Collections.Generic;
using System.Linq;

namespace yQuant.Core.Models;

public class Account
{

    public required string Alias { get; set; } // Account alias (e.g., "Main_Aggressive", "Sub_Safe_01")
    public required string Number { get; set; }
    public required string Broker { get; set; }
    public required string AppKey { get; set; }
    public required string AppSecret { get; set; }
    public required Dictionary<CurrencyType, decimal> Deposits { get; set; } = new Dictionary<CurrencyType, decimal>();
    public List<Position> Positions { get; set; } = new List<Position>();
    public required bool Active { get; set; }

    public decimal GetTotalEquity(CurrencyType targetCurrency)
    {
        var totalEquity = Deposits.GetValueOrDefault(targetCurrency);
        totalEquity += Positions
            .Where(p => p.Currency == targetCurrency)
            .Sum(p => p.CurrentPrice * p.Qty);
        return totalEquity;
    }
}