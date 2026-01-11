using System.Text.Json;
using Microsoft.Extensions.Options;
using yQuant.Infra.Notification;
using yQuant.Infra.Notification.Discord;
using yQuant.Infra.Notification.Telegram;
using yQuant.App.Notifier.Configuration;

namespace yQuant.App.Notifier.Services;

/// <summary>
/// 메시지를 적절한 채널(Discord/Telegram)로 라우팅
/// </summary>
public class MessageRouter
{
    private readonly NotifierConfiguration _config;
    private readonly DiscordLogger _discordLogger;
    private readonly TelegramNotificationService? _telegramService;
    private readonly ILogger<MessageRouter> _logger;

    public MessageRouter(
        IOptions<NotifierConfiguration> config,
        DiscordLogger discordLogger,
        TelegramNotificationService? telegramService,
        ILogger<MessageRouter> logger)
    {
        _config = config.Value;
        _discordLogger = discordLogger;
        _telegramService = telegramService;
        _logger = logger;
    }

    public async Task RouteMessageAsync(string channel, string messageJson)
    {
        try
        {
            var message = JsonSerializer.Deserialize<NotificationMessage>(messageJson);
            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message from channel {Channel}", channel);
                return;
            }

            message.Channel = channel;

            var routing = GetRoutingConfig(channel);
            if (routing.Targets.Length == 0)
            {
                _logger.LogDebug("No targets configured for channel {Channel}, skipping", channel);
                return;
            }

            var tasks = new List<Task>();

            foreach (var target in routing.Targets)
            {
                switch (target.ToLowerInvariant())
                {
                    case "discord":
                        if (_config.Discord.Enabled)
                            tasks.Add(SendToDiscordAsync(message));
                        break;

                    case "telegram":
                        if (_config.Telegram.Enabled && _telegramService != null)
                        {
                            // Telegram 필터 확인
                            if (ShouldSendToTelegram(message, routing))
                                tasks.Add(SendToTelegramAsync(message));
                            else
                                _logger.LogDebug("Message type {Type} filtered out for Telegram", message.Type);
                        }
                        break;

                    default:
                        _logger.LogWarning("Unknown target: {Target}", target);
                        break;
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing message from channel {Channel}", channel);
        }
    }

    private bool ShouldSendToTelegram(NotificationMessage message, RoutingTargetConfiguration routing)
    {
        // 필터가 비어있으면 모든 메시지 전송
        if (routing.TelegramFilter.Length == 0)
            return true;

        // 필터에 메시지 타입이 포함되어 있는지 확인
        return routing.TelegramFilter.Contains(message.Type, StringComparer.OrdinalIgnoreCase);
    }

    private async Task SendToDiscordAsync(NotificationMessage message)
    {
        try
        {
            // Use the type directly as context to avoid redundant channel info in the title
            var context = message.Type;
            var messageText = FormatMessage(message);

            if (message.Channel == NotificationChannels.Security)
            {
                await _discordLogger.LogSecurityAsync(context, messageText);
            }
            else if (message.Type.Equals("Catalog", StringComparison.OrdinalIgnoreCase))
            {
                await _discordLogger.LogCatalogAsync(context, messageText);
            }
            else
            {
                await _discordLogger.LogStatusAsync(context, messageText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Discord");
        }
    }

    private async Task SendToTelegramAsync(NotificationMessage message)
    {
        try
        {
            if (_telegramService == null)
            {
                _logger.LogWarning("Telegram service is not configured");
                return;
            }

            var messageText = FormatTelegramMessage(message);
            await _telegramService.SendNotificationAsync(messageText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Telegram");
        }
    }

    private string FormatMessage(NotificationMessage message)
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(message.AccountAlias))
            lines.Add($"**Account**: {message.AccountAlias}");

        // If data is a string or a JsonElement representing a string, display it directly
        if (message.Data is string str)
        {
            lines.Add(str);
        }
        else if (message.Data is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                lines.Add(element.GetString() ?? "");
            }
            else
            {
                lines.Add(JsonSerializer.Serialize(message.Data, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        else
        {
            lines.Add(JsonSerializer.Serialize(message.Data, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        return string.Join("\n", lines);
    }

    private string FormatTelegramMessage(NotificationMessage message)
    {
        // Telegram용 간결한 포맷
        var lines = new List<string>
        {
            $"🔔 {message.Type}"
        };

        if (!string.IsNullOrEmpty(message.AccountAlias))
            lines.Add($"📊 Account: {message.AccountAlias}");

        lines.Add($"⏰ {message.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");

        // 데이터는 간략하게
        var dataJson = JsonSerializer.Serialize(message.Data, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // 너무 길면 자르기
        if (dataJson.Length > 200)
            dataJson = dataJson.Substring(0, 197) + "...";

        lines.Add($"📝 {dataJson}");

        return string.Join("\n", lines);
    }

    private RoutingTargetConfiguration GetRoutingConfig(string channel)
    {
        return channel switch
        {
            NotificationChannels.Orders => _config.MessageRouting.Orders,
            NotificationChannels.Schedules => _config.MessageRouting.Schedules,
            NotificationChannels.Positions => _config.MessageRouting.Positions,
            NotificationChannels.System => _config.MessageRouting.System,
            NotificationChannels.Security => _config.MessageRouting.System, // Use System routing config for Security as well
            _ => new RoutingTargetConfiguration { Targets = new[] { "Discord" } }
        };
    }
}
