using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace yQuant.Infra.Valkey.Services;

public class HeartbeatService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly string _serviceName;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(15);
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(30);

    public HeartbeatService(
        IConnectionMultiplexer redis,
        ILogger<HeartbeatService> logger,
        string serviceName)
    {
        _redis = redis;
        _logger = logger;
        _serviceName = serviceName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatService started for {ServiceName}", _serviceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"status:heartbeat:{_serviceName}";
                await db.StringSetAsync(key, DateTime.UtcNow.ToString("o"), _ttl);

                // Optional: Log only on debug to avoid spam
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Heartbeat sent for {ServiceName}", _serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat for {ServiceName}", _serviceName);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
