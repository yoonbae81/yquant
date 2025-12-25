using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

/// <summary>
/// 일별 계좌 스냅샷 저장소 인터페이스
/// 계좌별 DailySnapshot.jsonl 파일에 일자별로 한 줄씩 저장
/// </summary>
public interface IDailySnapshotRepository
{
    /// <summary>
    /// 일별 스냅샷 저장 (Append-only)
    /// ~/yQuant/history/{accountAlias}/DailySnapshot.jsonl
    /// </summary>
    Task SaveAsync(string accountAlias, DailySnapshot snapshot);

    /// <summary>
    /// 특정 날짜의 스냅샷 조회
    /// </summary>
    Task<DailySnapshot?> GetSnapshotByDateAsync(string accountAlias, DateOnly date);

    /// <summary>
    /// 기간별 스냅샷 조회
    /// </summary>
    Task<IEnumerable<DailySnapshot>> GetSnapshotsByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// 계좌별 전체 스냅샷 조회 (시계열 순)
    /// </summary>
    Task<IEnumerable<DailySnapshot>> GetAllSnapshotsAsync(string accountAlias);

    /// <summary>
    /// 최신 스냅샷 조회 (가장 최근 날짜)
    /// </summary>
    Task<DailySnapshot?> GetLatestSnapshotAsync(string accountAlias);
}
