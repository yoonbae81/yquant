using System.Text.Json;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Repositories;

/// <summary>
/// JSONL 기반 거래 실적 저장소
/// 계좌별 디렉토리에 일자별 파일로 저장: ~/yQuant/history/{account}/YYYY-MM-DD.jsonl
/// </summary>
public class JsonLinesTradeRepository : ITradeRepository
{
    private readonly string _baseDirectory;
    private readonly ILogger<JsonLinesTradeRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public JsonLinesTradeRepository(
        ILogger<JsonLinesTradeRepository> logger,
        string? baseDirectory = null)
    {
        _logger = logger;
        _baseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "yQuant",
            "history");

        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task SaveAsync(string accountAlias, TradeRecord trade)
    {
        var accountDir = Path.Combine(_baseDirectory, accountAlias);
        Directory.CreateDirectory(accountDir);

        var date = DateOnly.FromDateTime(trade.ExecutedAt);
        var filePath = Path.Combine(accountDir, $"{date:yyyy-MM-dd}.jsonl");

        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(trade, _jsonOptions);
            await File.AppendAllTextAsync(filePath, json + "\n");

            _logger.LogInformation(
                "Saved trade {TradeId} for {Account} to {File}",
                trade.Id, accountAlias, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save trade {TradeId} for {Account}", trade.Id, accountAlias);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByDateAsync(string accountAlias, DateOnly date)
    {
        var filePath = Path.Combine(_baseDirectory, accountAlias, $"{date:yyyy-MM-dd}.jsonl");

        if (!File.Exists(filePath))
        {
            return Enumerable.Empty<TradeRecord>();
        }

        return await ReadJsonLinesAsync(filePath);
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByRangeAsync(
        string accountAlias,
        DateOnly startDate,
        DateOnly endDate)
    {
        var trades = new List<TradeRecord>();
        var accountDir = Path.Combine(_baseDirectory, accountAlias);

        if (!Directory.Exists(accountDir))
        {
            return trades;
        }

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dailyTrades = await GetTradesByDateAsync(accountAlias, date);
            trades.AddRange(dailyTrades);
        }

        return trades.OrderBy(t => t.ExecutedAt);
    }

    public async Task<IEnumerable<TradeRecord>> GetAllTradesAsync(string accountAlias)
    {
        var accountDir = Path.Combine(_baseDirectory, accountAlias);

        if (!Directory.Exists(accountDir))
        {
            return Enumerable.Empty<TradeRecord>();
        }

        var trades = new List<TradeRecord>();
        var files = Directory.GetFiles(accountDir, "*.jsonl")
            .Where(f => !f.EndsWith("DailySnapshot.jsonl"))
            .OrderBy(f => f);

        foreach (var file in files)
        {
            var dailyTrades = await ReadJsonLinesAsync(file);
            trades.AddRange(dailyTrades);
        }

        return trades.OrderBy(t => t.ExecutedAt);
    }

    private async Task<List<TradeRecord>> ReadJsonLinesAsync(string filePath)
    {
        var trades = new List<TradeRecord>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var trade = JsonSerializer.Deserialize<TradeRecord>(line);
                    if (trade != null)
                    {
                        trades.Add(trade);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse trade record from line: {Line}", line);
                    // 손상된 줄은 건너뛰고 계속 진행
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read trades from {FilePath}", filePath);
        }

        return trades;
    }
}
