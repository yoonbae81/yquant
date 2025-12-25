namespace yQuant.Core.Models;

/// <summary>
/// 일별 계좌 스냅샷
/// 매일 장 마감 후 계좌 상태를 기록하여 성과 지표 산출에 사용
/// </summary>
public class DailySnapshot
{
    /// <summary>기준 날짜</summary>
    public required DateOnly Date { get; set; }

    /// <summary>통화</summary>
    public required CurrencyType Currency { get; set; }

    /// <summary>총 자산 (현금 + 평가금액)</summary>
    public required decimal TotalEquity { get; set; }

    /// <summary>현금 잔고</summary>
    public required decimal CashBalance { get; set; }

    /// <summary>포지션 평가 금액</summary>
    public required decimal PositionValue { get; set; }

    /// <summary>일간 손익 (전일 대비)</summary>
    public required decimal DailyPnL { get; set; }

    /// <summary>일간 수익률 (전일 대비 %)</summary>
    public required double DailyReturn { get; set; }

    /// <summary>누적 수익률 (초기 자산 대비 %)</summary>
    public double CumulativeReturn { get; set; }

    /// <summary>보유 포지션 개수</summary>
    public required int PositionsCount { get; set; }

    /// <summary>최고점 대비 낙폭 (Drawdown %)</summary>
    public double DrawdownPct { get; set; }

    /// <summary>보유 포지션 상세 (해당 통화)</summary>
    public List<Position> Positions { get; set; } = new();
}
