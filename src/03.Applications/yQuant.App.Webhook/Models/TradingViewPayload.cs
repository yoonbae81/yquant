namespace yQuant.App.Webhook.Models;

public class TradingViewPayload
{
    public string? Ticker { get; set; }
    public string? Action { get; set; }
    public decimal? Strength { get; set; }
    public decimal? Price { get; set; }
    public string? Exchange { get; set; } // "NASDAQ", "KRX"
    public string? Currency { get; set; } // "currency" (String) -> CurrencyType (Enum)
    public string? Strategy { get; set; } // 매수 전략명 (TradingView Strategy Name)
    public string? Secret { get; set; }
}
