using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

/// <summary>
/// 거래 실적 저장소 인터페이스
/// 계좌별 디렉토리에 일자별 JSONL 파일로 저장
/// </summary>
public interface ITradeRepository
{
    /// <summary>
    /// 거래 기록 저장 (Append-only)
    /// ~/yQuant/history/{accountAlias}/YYYY-MM-DD.jsonl
    /// </summary>
    Task SaveAsync(string accountAlias, TradeRecord trade);

    /// <summary>
    /// 특정 날짜의 모든 거래 조회
    /// </summary>
    Task<IEnumerable<TradeRecord>> GetTradesByDateAsync(string accountAlias, DateOnly date);

    /// <summary>
    /// 기간별 거래 조회
    /// </summary>
    Task<IEnumerable<TradeRecord>> GetTradesByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// 계좌별 전체 거래 조회
    /// </summary>
    Task<IEnumerable<TradeRecord>> GetAllTradesAsync(string accountAlias);
}
