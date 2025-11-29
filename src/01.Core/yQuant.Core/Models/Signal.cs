namespace yQuant.Core.Models;

public class Signal
{
    public Guid Id { get; set; }
    public required string Ticker { get; set; }
    public required string Exchange { get; set; }
    public CurrencyType? Currency { get; set; } // Nullable as per doc - "N" in required column
    public required OrderAction Action { get; set; }
    public decimal? Price { get; set; }
    public int? Strength { get; set; }
    public required string Source { get; set; }
    public DateTime Timestamp { get; set; }

    public Signal()
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
    }
}