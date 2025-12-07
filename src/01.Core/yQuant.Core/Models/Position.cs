using System.Runtime.InteropServices.JavaScript;

namespace yQuant.Core.Models;

public class Position
{
    public required string AccountAlias { get; set; }
    public required string Ticker { get; set; }
    public required CurrencyType Currency { get; set; }
    public required decimal Qty { get; set; }
    public required decimal AvgPrice { get; set; }
    public required decimal CurrentPrice { get; set; }
    public decimal ChangeRate { get; set; }
    public string? Source { get; set; }
    public ExchangeCode? Exchange { get; set; }

    // Calculated Property
    public decimal UnrealizedPnL => (CurrentPrice - AvgPrice) * Qty;
}
