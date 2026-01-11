using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

/// <summary>
/// Background service that consumes trades from Valkey Queue and archives them to Firebird.
/// </summary>
public class TradeArchiverService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ITradeRepository _repository; // This should be the Firebird repository
    private readonly ILogger<TradeArchiverService> _logger;
    private const string QueueKey = "trades:queue";
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TradeArchiverService(
        IConnectionMultiplexer redis,
        ITradeRepository repository,
        ILogger<TradeArchiverService> logger)
    {
        _redis = redis;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradeArchiverService started. Listening to {QueueKey}", QueueKey);

        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Blocking Pop: Wait up to 5 seconds for a new item
                // Use BLPOP (RightPop) to process FIFO if pushed with LPUSH (LeftPush)
                // Or BRPOP if pushed with RPUSH (RightPush). 
                // Let's assume standard Queue: LPUSH (Producer) -> RPOP (Consumer).
                // So BRPOP is correct for picking from the tail (oldest).

                var result = await db.ListRightPopAsync(QueueKey); // Non-blocking version first? No, use blocking if possible.
                                                                   // StackExchange.Redis doesn't expose blocking pop directly in a friendly async way without potentially blocking threads?
                                                                   // Actually ListRightPopAsync is non-blocking. 
                                                                   // To do blocking efficiently without holding a thread, we can use a loop with short delay or just poll.
                                                                   // Since this is a BackgroundService, polling with small Backoff is fine.

                if (result.HasValue)
                {
                    await ProcessTradeMessageAsync(result.ToString());
                }
                else
                {
                    await Task.Delay(100, stoppingToken); // 100ms idle wait
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TradeArchiverService loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("TradeArchiverService stopped.");
    }

    private async Task ProcessTradeMessageAsync(string message)
    {
        try
        {
            // Message format: { "AccountAlias": "...", "Trade": { ... } }
            // Or just TradeRecord with added AccountAlias field?
            // TradeRecord doesn't have AccountAlias.
            // So we need a wrapper or dynamic object.

            var payload = JsonSerializer.Deserialize<TradeQueueItem>(message, _jsonOptions);

            if (payload != null && payload.Trade != null && !string.IsNullOrEmpty(payload.AccountAlias))
            {
                await _repository.SaveAsync(payload.AccountAlias, payload.Trade);
                _logger.LogInformation("Archived trade {TradeId} for {Account}", payload.Trade.Id, payload.AccountAlias);
            }
            else
            {
                _logger.LogWarning("Received invalid trade queue item: {Message}", message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process trade message: {Message}", message);
            // TODO: Push back to Dead Letter Queue?
        }
    }
}

public class TradeQueueItem
{
    public string AccountAlias { get; set; } = string.Empty;
    public TradeRecord Trade { get; set; } = default!;
}
