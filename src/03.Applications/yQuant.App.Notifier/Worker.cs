using StackExchange.Redis;
using yQuant.Infra.Notification;
using yQuant.App.Notifier.Services;

namespace yQuant.App.Notifier;

/// <summary>
/// Valkey 채널을 구독하여 알림 메시지를 처리하는 백그라운드 워커
/// </summary>
public class Worker : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly MessageRouter _router;
    private readonly ILogger<Worker> _logger;
    private ISubscriber? _subscriber;

    public Worker(
        IConnectionMultiplexer redis,
        MessageRouter router,
        ILogger<Worker> logger)
    {
        _redis = redis;
        _router = router;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notifier Worker starting...");

        try
        {
            _subscriber = _redis.GetSubscriber();

            // 모든 알림 채널 구독
            foreach (var channel in NotificationChannels.All)
            {
                await _subscriber.SubscribeAsync(
                    RedisChannel.Literal(channel),
                    async (ch, message) =>
                    {
                        _logger.LogDebug("Received message from channel {Channel}", ch);
                        await _router.RouteMessageAsync(ch.ToString(), message.ToString());
                    });

                _logger.LogInformation("Subscribed to channel: {Channel}", channel);
            }

            _logger.LogInformation("Notifier Worker is running and listening to {Count} channels",
                NotificationChannels.All.Length);

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Notifier Worker is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Notifier Worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Notifier Worker stopping...");

        if (_subscriber != null)
        {
            foreach (var channel in NotificationChannels.All)
            {
                await _subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
                _logger.LogInformation("Unsubscribed from channel: {Channel}", channel);
            }
        }

        await base.StopAsync(cancellationToken);
    }
}
