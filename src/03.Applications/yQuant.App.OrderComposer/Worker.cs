using System.Reflection;
using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Input;
using yQuant.Core.Ports.Output.Policies;

namespace yQuant.App.OrderComposer
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IOrderCompositionUseCase _orderCompositionUseCase;

        public Worker(
            ILogger<Worker> logger, 
            IConnectionMultiplexer redis, 
            IOrderCompositionUseCase orderCompositionUseCase)
        {
            _logger = logger;
            _redis = redis;
            _orderCompositionUseCase = orderCompositionUseCase;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderComposer Worker started at: {time}", DateTimeOffset.Now);

            var subscriber = _redis.GetSubscriber();
            await subscriber.SubscribeAsync(RedisChannel.Literal("signal"), async (channel, message) =>
            {
                _logger.LogInformation("Received signal: {Message}", message);
                try
                {
                    var signal = JsonSerializer.Deserialize<Signal>(message.ToString());
                    if (signal == null)
                    {
                        _logger.LogWarning("Failed to deserialize signal: {Message}", message);
                        return;
                    }

                    await _orderCompositionUseCase.ProcessSignalAsync(signal);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing signal: {Message}", message);
                }
            });

            // Keep the worker running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogInformation("OrderComposer Worker stopped at: {time}", DateTimeOffset.Now);
        }
    }
}