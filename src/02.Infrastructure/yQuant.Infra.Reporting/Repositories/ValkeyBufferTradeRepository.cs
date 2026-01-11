using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Repositories;

/// <summary>
/// Valkey Queue-based Trade Repository (Write-Behind Pattern)
/// Only supports SaveAsync (Pushes to Queue).
/// Read methods are not supported as this is an ingestion-only adapter.
/// </summary>
public class ValkeyBufferTradeRepository : ITradeRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeyBufferTradeRepository> _logger;
    private const string QueueKey = "trades:queue";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public ValkeyBufferTradeRepository(
        IConnectionMultiplexer redis,
        ILogger<ValkeyBufferTradeRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task SaveAsync(string accountAlias, TradeRecord trade)
    {
        try
        {
            var db = _redis.GetDatabase();

            // We reuse the TradeQueueItem class from Infra.Persistence if possible, 
            // OR define a local DTO to avoid circular dependency if Persistence depends on Reporting (unlikely).
            // Actually Persistence depends on Core. Reporting depends on Core.
            // But they don't depend on each other usually.
            // So we should define `TradeQueueItem` in Core or duplicate it/use anonymous type if simple.
            // Let's use anonymous object for serialization to keep it simple and decoupled.

            var payload = new
            {
                AccountAlias = accountAlias,
                Trade = trade
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            // LPUSH for high throughput
            await db.ListLeftPushAsync(QueueKey, json);

            _logger.LogInformation("Buffered trade {TradeId} for {Account}", trade.Id, accountAlias);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to buffer trade {TradeId} for {Account}", trade.Id, accountAlias);
            // Optimization: If Redis fails, maybe fallback to log file?
            throw;
        }
    }

    public Task<IEnumerable<TradeRecord>> GetAllTradesAsync(string accountAlias)
    {
        throw new NotSupportedException("ValkeyBufferTradeRepository is for ingestion only. Use FirebirdTradeRepository for queries.");
    }

    public Task<IEnumerable<TradeRecord>> GetTradesByDateAsync(string accountAlias, DateOnly date)
    {
        throw new NotSupportedException("ValkeyBufferTradeRepository is for ingestion only. Use FirebirdTradeRepository for queries.");
    }

    public Task<IEnumerable<TradeRecord>> GetTradesByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate)
    {
        throw new NotSupportedException("ValkeyBufferTradeRepository is for ingestion only. Use FirebirdTradeRepository for queries.");
    }
}
