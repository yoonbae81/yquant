using System.Reflection;
using System.Text.Json;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Input;
using yQuant.Core.Ports.Output.Policies;
using yQuant.App.OrderManager.Services;

namespace yQuant.App.OrderManager
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ScheduleExecutor _scheduleExecutor;

        public Worker(
            ILogger<Worker> logger,
            IConnectionMultiplexer redis,
            IServiceScopeFactory serviceScopeFactory,
            ScheduleExecutor scheduleExecutor)
        {
            _logger = logger;
            _redis = redis;
            _serviceScopeFactory = serviceScopeFactory;
            _scheduleExecutor = scheduleExecutor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderManager Worker started at: {time}", DateTimeOffset.Now);

            // Subscribe to signal channel
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

                    // Create a scope to resolve scoped services
                    using var scope = _serviceScopeFactory.CreateScope();
                    var orderCompositionUseCase = scope.ServiceProvider.GetRequiredService<IOrderCompositionUseCase>();
                    await orderCompositionUseCase.ProcessSignalAsync(signal);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing signal: {Message}", message);
                }
            });

            _logger.LogInformation("Subscribed to 'signal' channel");

            // Periodic schedule checking (every 10 seconds)
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await _scheduleExecutor.ProcessSchedulesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in schedule executor");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("OrderManager Worker stopping due to cancellation");
            }

            _logger.LogInformation("OrderManager Worker stopped at: {time}", DateTimeOffset.Now);
        }
    }
}