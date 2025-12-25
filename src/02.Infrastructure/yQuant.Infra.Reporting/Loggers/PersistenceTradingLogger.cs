using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Reporting.Loggers;

public class PersistenceTradingLogger : ITradingLogger
{
    private readonly IPerformanceRepository _repository;
    private readonly ILogger<PersistenceTradingLogger> _logger;

    public PersistenceTradingLogger(IPerformanceRepository repository, ILogger<PersistenceTradingLogger> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task LogSignalAsync(Signal signal, string timeframe = "1d")
    {
        // No-op for persistence logger
        return Task.CompletedTask;
    }

    public Task LogOrderAsync(Order order)
    {
        // No-op for persistence logger
        return Task.CompletedTask;
    }

    public Task LogOrderFailureAsync(Order order, string reason)
    {
        // No-op for persistence logger
        return Task.CompletedTask;
    }

    public Task LogAccountErrorAsync(string accountAlias, Exception ex, string context)
    {
        // No-op for persistence logger
        return Task.CompletedTask;
    }

    public async Task LogReportAsync(string accountAlias, PerformanceLog summary)
    {
        try
        {
            await _repository.SaveAsync(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist summary log for {AccountAlias}", accountAlias);
        }
    }
}
