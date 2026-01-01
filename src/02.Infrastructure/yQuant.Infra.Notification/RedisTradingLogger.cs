using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Notification
{
    public class RedisTradingLogger : ITradingLogger
    {
        private readonly NotificationPublisher _publisher;

        public RedisTradingLogger(NotificationPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task LogSignalAsync(Signal signal, string timeframe = "1d")
        {
            await _publisher.PublishAsync(NotificationChannels.Orders, "Signal", null, new { Signal = signal, Timeframe = timeframe });
        }

        public async Task LogOrderAsync(Order order)
        {
            await _publisher.PublishOrderNotificationAsync("OrderExecuted", order.AccountAlias, order);
        }

        public async Task LogOrderFailureAsync(Order order, string reason)
        {
            await _publisher.PublishOrderNotificationAsync("OrderFailed", order.AccountAlias, new { Order = order, Reason = reason });
        }

        public async Task LogAccountErrorAsync(string accountAlias, Exception ex, string context)
        {
            await _publisher.PublishSystemNotificationAsync("AccountError", new { AccountAlias = accountAlias, Context = context, Exception = ex.Message });
        }

        public async Task LogReportAsync(string accountAlias, PerformanceLog summary)
        {
            await _publisher.PublishSystemNotificationAsync("DailySummary", new { AccountAlias = accountAlias, Summary = summary });
        }
    }
}
