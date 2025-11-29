namespace yQuant.Core.Models;

public class Order
{
    public Guid Id { get; set; }
    public required string AccountAlias { get; set; }
    public required string Ticker { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public CurrencyType Currency { get; set; } = CurrencyType.USD;
    public required OrderAction Action { get; set; }
    public required OrderType Type { get; set; }
    public required decimal Qty { get; set; }
    public decimal? Price { get; set; } // Nullable for market orders
    public DateTime Timestamp { get; set; }

    public Order()
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
    }
}