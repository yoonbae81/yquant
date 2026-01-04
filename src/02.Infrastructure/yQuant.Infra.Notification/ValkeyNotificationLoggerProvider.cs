using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace yQuant.Infra.Notification
{
    public class ValkeyNotificationLoggerProvider : ILoggerProvider
    {
        private readonly NotificationPublisher _publisher;
        private readonly ConcurrentDictionary<string, ValkeyNotificationLogger> _loggers = new();

        public ValkeyNotificationLoggerProvider(NotificationPublisher publisher)
        {
            _publisher = publisher;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new ValkeyNotificationLogger(name, _publisher));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    public class ValkeyNotificationLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly NotificationPublisher _publisher;

        public ValkeyNotificationLogger(string categoryName, NotificationPublisher publisher)
        {
            _categoryName = categoryName;
            _publisher = publisher;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == NotificationEvents.Security.Id)
            {
                var message = formatter(state, exception);
                // Extract alias from state if it's a scope/dictionary
                string? alias = null;
                if (state is IEnumerable<KeyValuePair<string, object>> scope)
                {
                    alias = scope.FirstOrDefault(x => x.Key == "AccountAlias").Value?.ToString();
                }

                _ = _publisher.PublishSecurityNotificationAsync(message, new { Category = _categoryName, Alias = alias });
            }
            else if (eventId.Id == NotificationEvents.System.Id)
            {
                var message = formatter(state, exception);
                _ = _publisher.PublishSystemNotificationAsync(message, new { Category = _categoryName });
            }
        }
    }

    public static class ValkeyNotificationLoggerExtensions
    {
        public static ILoggingBuilder AddValkeyNotification(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, ValkeyNotificationLoggerProvider>();
            return builder;
        }
    }
}
