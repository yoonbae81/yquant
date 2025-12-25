using System.Text.Json;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Repositories;

/// <summary>
/// JSONL 기반 일별 스냅샷 저장소
/// 계좌별 DailySnapshot.jsonl 파일에 일자별로 한 줄씩 저장
/// </summary>
public class JsonLinesDailySnapshotRepository : IDailySnapshotRepository
{
    private readonly string _baseDirectory;
    private readonly ILogger<JsonLinesDailySnapshotRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private const string SnapshotFileName = "DailySnapshot.jsonl";

    public JsonLinesDailySnapshotRepository(
        ILogger<JsonLinesDailySnapshotRepository> logger,
        string? baseDirectory = null)
    {
        _logger = logger;
        _baseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "yQuant",
            "history");

        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task SaveAsync(string accountAlias, DailySnapshot snapshot)
    {
        var accountDir = Path.Combine(_baseDirectory, accountAlias);
        Directory.CreateDirectory(accountDir);

        var filePath = Path.Combine(accountDir, SnapshotFileName);

        await _lock.WaitAsync();
        try
        {
            // 중복 방지: 같은 날짜가 이미 있는지 확인
            var existing = await GetSnapshotByDateAsync(accountAlias, snapshot.Date);
            if (existing != null)
            {
                _logger.LogWarning(
                    "Snapshot for {Account} on {Date} already exists. Skipping.",
                    accountAlias, snapshot.Date);
                return;
            }

            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            await File.AppendAllTextAsync(filePath, json + "\n");

            _logger.LogInformation(
                "Saved daily snapshot for {Account} on {Date}: Equity={Equity:N2}, Return={Return:P2}",
                accountAlias, snapshot.Date, snapshot.TotalEquity, snapshot.DailyReturn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snapshot for {Account} on {Date}", accountAlias, snapshot.Date);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DailySnapshot?> GetSnapshotByDateAsync(string accountAlias, DateOnly date)
    {
        var snapshots = await GetAllSnapshotsAsync(accountAlias);
        return snapshots.FirstOrDefault(s => s.Date == date);
    }

    public async Task<IEnumerable<DailySnapshot>> GetSnapshotsByRangeAsync(
        string accountAlias,
        DateOnly startDate,
        DateOnly endDate)
    {
        var snapshots = await GetAllSnapshotsAsync(accountAlias);
        return snapshots
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .OrderBy(s => s.Date);
    }

    public async Task<IEnumerable<DailySnapshot>> GetAllSnapshotsAsync(string accountAlias)
    {
        var filePath = Path.Combine(_baseDirectory, accountAlias, SnapshotFileName);

        if (!File.Exists(filePath))
        {
            return Enumerable.Empty<DailySnapshot>();
        }

        return await ReadJsonLinesAsync(filePath);
    }

    public async Task<DailySnapshot?> GetLatestSnapshotAsync(string accountAlias)
    {
        var snapshots = await GetAllSnapshotsAsync(accountAlias);
        return snapshots.OrderByDescending(s => s.Date).FirstOrDefault();
    }

    private async Task<List<DailySnapshot>> ReadJsonLinesAsync(string filePath)
    {
        var snapshots = new List<DailySnapshot>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var snapshot = JsonSerializer.Deserialize<DailySnapshot>(line);
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse snapshot from line: {Line}", line);
                    // 손상된 줄은 건너뛰고 계속 진행
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read snapshots from {FilePath}", filePath);
        }

        return snapshots.OrderBy(s => s.Date).ToList();
    }
}
