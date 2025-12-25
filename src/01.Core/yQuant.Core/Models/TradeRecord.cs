namespace yQuant.Core.Models;

/// <summary>
/// 거래 실적 기록 (Trade Execution Record)
/// 성과 지표 산출을 위한 모든 거래 체결 내역을 저장
/// </summary>
public class TradeRecord
{
    /// <summary>거래 고유 ID</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>체결 시각 (UTC)</summary>
    public required DateTime ExecutedAt { get; set; }

    /// <summary>종목 코드</summary>
    public required string Ticker { get; set; }

    /// <summary>거래 방향 (Buy/Sell)</summary>
    public required OrderAction Action { get; set; }

    /// <summary>체결 수량</summary>
    public required decimal Quantity { get; set; }

    /// <summary>체결 가격</summary>
    public required decimal ExecutedPrice { get; set; }

    /// <summary>거래 금액 (수량 × 가격)</summary>
    public decimal Amount => Quantity * ExecutedPrice;

    /// <summary>수수료</summary>
    public decimal Commission { get; set; }

    /// <summary>내부 주문 ID</summary>
    public string? OrderId { get; set; }

    /// <summary>증권사 주문 ID (ODNO)</summary>
    public string? BrokerOrderId { get; set; }

    /// <summary>전략/사유 (Manual, Schedule, Webhook:{StrategyName})</summary>
    public string? Strategy { get; set; }

    /// <summary>통화</summary>
    public CurrencyType Currency { get; set; }

    /// <summary>거래소</summary>
    public ExchangeCode Exchange { get; set; }
}
