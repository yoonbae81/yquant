using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Performance.Repositories;

public class JsonPerformanceRepository : IPerformanceRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonPerformanceRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonPerformanceRepository(ILogger<JsonPerformanceRepository> logger, string filePath = "performance_logs.json")
    {
        _logger = logger;
        _filePath = filePath;
    }

    public async Task SaveAsync(PerformanceLog log)
    {
        await _lock.WaitAsync();
        try
        {
            var logs = await LoadLogsInternalAsync();
            logs.Add(log);
            await SaveLogsInternalAsync(logs);
            _logger.LogInformation("Saved performance log for {AccountAlias} on {Date}", log.AccountAlias, log.Date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save performance log");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<PerformanceLog>> GetLogsAsync(string accountAlias)
    {
        await _lock.WaitAsync();
        try
        {
            var logs = await LoadLogsInternalAsync();
            return logs.Where(l => l.AccountAlias == accountAlias).OrderBy(l => l.Date);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<PerformanceLog>> LoadLogsInternalAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<PerformanceLog>();
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<PerformanceLog>>(stream) ?? new List<PerformanceLog>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load existing performance logs. Starting fresh.");
            return new List<PerformanceLog>();
        }
    }

    private async Task SaveLogsInternalAsync(List<PerformanceLog> logs)
    {
        using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, logs, new JsonSerializerOptions { WriteIndented = true });
    }
}
