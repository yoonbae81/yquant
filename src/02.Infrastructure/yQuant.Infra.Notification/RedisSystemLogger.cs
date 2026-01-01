using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Notification
{
    public class RedisSystemLogger : ISystemLogger
    {
        private readonly NotificationPublisher _publisher;

        public RedisSystemLogger(NotificationPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task LogStartupAsync(string appName, string version)
        {
            await _publisher.PublishSystemNotificationAsync("Startup", new { AppName = appName, Version = version });
        }

        public async Task LogSystemErrorAsync(string context, Exception ex)
        {
            await _publisher.PublishSystemNotificationAsync("SystemError", new { Context = context, Exception = ex.Message });
        }

        public async Task LogStatusAsync(string context, string message)
        {
            await _publisher.PublishSystemNotificationAsync("Status", new { Context = context, Message = message });
        }

        public async Task LogSecurityAsync(string context, string message)
        {
            await _publisher.PublishSecurityNotificationAsync(context, message);
        }
    }
}
