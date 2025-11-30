namespace yQuant.App.TradingViewWebhook.Models;

public class TradingViewPayload
{
    public string? Ticker { get; set; }
    public string? Action { get; set; }
    public decimal? Strength { get; set; }
    public decimal? Price { get; set; }
    public string? Exchange { get; set; } // "NASDAQ", "KRX"
    public string? Currency { get; set; } // "currency" (String) -> CurrencyType (Enum)
    public string? Comment { get; set; } // 매수 계기 (Source)
    public string? Secret { get; set; }
}
