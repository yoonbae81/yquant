using Microsoft.Extensions.Logging;

namespace yQuant.Infra.Notification
{
    public static class NotificationEvents
    {
        public static readonly EventId Security = new EventId(1001, "SecurityNotification");
        public static readonly EventId System = new EventId(1002, "SystemNotification");
    }

    public static class NotificationLoggerExtensions
    {
        public static void LogSecurityNotification(this ILogger logger, string message, string? accountAlias = null)
        {
            // Use Scope to pass accountAlias if needed, or structured logging
            using (logger.BeginScope(new Dictionary<string, object> { ["AccountAlias"] = accountAlias ?? "" }))
            {
                logger.LogInformation(NotificationEvents.Security, message);
            }
        }

        public static void LogSystemNotification(this ILogger logger, string message)
        {
            logger.LogInformation(NotificationEvents.System, message);
        }
    }
}
