using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.App.Web.Services
{
    public class ExecutionListener : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly RealtimeEventService _eventService;
        private readonly ILogger<ExecutionListener> _logger;

        public ExecutionListener(
            IConnectionMultiplexer redis,
            RealtimeEventService eventService,
            ILogger<ExecutionListener> logger)
        {
            _redis = redis;
            _eventService = eventService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExecutionListener started. Subscribing to 'execution' channel...");

            var subscriber = _redis.GetSubscriber();

            // Subscribe to channel
            await subscriber.SubscribeAsync(RedisChannel.Literal("execution"), (channel, message) =>
            {
                try
                {
                    var result = JsonSerializer.Deserialize<OrderResult>(message.ToString());
                    if (result != null)
                    {
                        _logger.LogInformation("Received execution result for Order {OrderId}: Success={Success}", result.OrderId, result.IsSuccess);
                        _eventService.NotifyOrderExecution(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle execution message.");
                }
            });

            // Keep the service running
            // Wait until cancellation is requested
            try
            {
                await Task.Delay(-1, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Verify logic: Unsubscribe on shutdown?
                // ConnectionMultiplexer handles disconnection, but explicit unsubscribe is polite.
                _logger.LogInformation("ExecutionListener stopping...");
            }
        }
    }
}
