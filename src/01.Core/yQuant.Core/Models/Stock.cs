namespace yQuant.Core.Models
{
    public class Stock
    {
        public string Ticker { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public CurrencyType Currency { get; set; }
    }
}
