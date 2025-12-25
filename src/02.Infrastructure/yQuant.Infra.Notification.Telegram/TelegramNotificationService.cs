using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Interfaces;
using System.Security.Cryptography;

namespace yQuant.Infra.Notification.Telegram;

public class TelegramNotificationService : INotificationService
{
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IRedisService _redisService;
    private readonly string _botToken;
    private readonly string _chatId;

    public TelegramNotificationService(ILogger<TelegramNotificationService> logger, IConfiguration configuration, HttpClient httpClient, IRedisService redisService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _redisService = redisService;
        _botToken = configuration["Notifier:Telegram:BotToken"]
                    ?? throw new InvalidOperationException("Notifier:Telegram:BotToken not configured.");

        _chatId = configuration["Notifier:Telegram:ChatId"]
                  ?? throw new InvalidOperationException("Notifier:Telegram:ChatId not configured.");

        _httpClient.BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/");
    }

    public async Task SendNotificationAsync(string message)
    {
        try
        {
            // Deduplication
            var messageHash = ComputeHash(message);
            var redisKey = $"Telegram:Dedup:{messageHash}";

            if (await _redisService.ExistsAsync(redisKey))
            {
                _logger.LogWarning("Duplicate Telegram message detected. Skipping. Message: {Message}", message);
                return;
            }

            // Set dedup key for 5 seconds
            await _redisService.SetAsync(redisKey, "1", TimeSpan.FromSeconds(5));

            var payload = new
            {
                chat_id = _chatId,
                text = message
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sendMessage", content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Telegram message sent successfully to ChatId: {ChatId}", _chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to ChatId: {ChatId}", _chatId);
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}