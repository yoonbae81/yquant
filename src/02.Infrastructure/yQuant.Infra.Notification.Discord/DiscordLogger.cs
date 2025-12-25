using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

using yQuant.Infra.Notification.Discord.Models;
using yQuant.Infra.Notification.Discord.Services;

namespace yQuant.Infra.Notification.Discord
{
    public class DiscordLogger : ITradingLogger, ISystemLogger
    {
        private readonly HttpClient _httpClient;
        private readonly DiscordConfiguration _config;
        private readonly ILogger<DiscordLogger> _logger;
        private readonly MessageBuilder _messageBuilder;

        public DiscordLogger(
            IHttpClientFactory httpClientFactory,
            IOptions<DiscordConfiguration> config,
            ILogger<DiscordLogger> logger,
            DiscordTemplateService templateService)
        {
            _httpClient = httpClientFactory.CreateClient("DiscordWebhook");
            _config = config.Value;
            _logger = logger;
            _messageBuilder = new MessageBuilder(templateService);
        }

        public async Task LogSignalAsync(Signal signal, string timeframe = "1d")
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetSignalWebhookUrl(signal);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildSignalMessage(signal, timeframe);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send signal log to Discord");
            }
        }

        public async Task LogOrderAsync(Order order)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(order.AccountAlias);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildExecutionMessage(order);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send execution log to Discord");
            }
        }

        public async Task LogOrderFailureAsync(Order order, string reason)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(order.AccountAlias); // Use execution channel for failures too? Or Error? Let's use Execution for now as it's order related.
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildOrderFailureMessage(order, reason);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order failure log to Discord");
            }
        }

        public async Task LogAccountErrorAsync(string accountAlias, Exception ex, string context)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(accountAlias);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildErrorMessage($"Account Error: {accountAlias}", ex, context);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to send account error log to Discord");
            }
        }

        public async Task LogReportAsync(string accountAlias, PerformanceLog summary)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(accountAlias);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildSummaryMessage(accountAlias, summary);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send summary log to Discord");
            }
        }

        public async Task LogStartupAsync(string appName, string version)
        {
            if (!_config.Enabled) return;

            try
            {
                string webhookUrl = _config.Channels.System?.Status ?? _config.Channels.Default ?? string.Empty;
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildStartupMessage(appName, version);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send startup log to Discord");
            }
        }

        public async Task LogSystemErrorAsync(string context, Exception ex)
        {
            if (!_config.Enabled) return;

            try
            {
                string webhookUrl = _config.Channels.System?.Error ?? _config.Channels.Default ?? string.Empty;
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildErrorMessage("System Error", ex, context);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to send system error log to Discord");
            }
        }

        public async Task LogStatusAsync(string context, string message)
        {
            if (!_config.Enabled) return;

            try
            {
                string webhookUrl = _config.Channels.System?.Status ?? _config.Channels.Default ?? string.Empty;
                if (string.IsNullOrEmpty(webhookUrl)) return;

                // Reuse Startup message builder or create a new one for generic status
                var payload = _messageBuilder.BuildStatusMessage(context, message);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status log to Discord");
            }
        }

        public async Task LogSecurityAsync(string context, string message)
        {
            if (!_config.Enabled) return;

            try
            {
                string webhookUrl = _config.Channels.System?.Security ?? _config.Channels.System?.Status ?? _config.Channels.Default ?? string.Empty;
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildStatusMessage(context, message);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send security log to Discord");
            }
        }

        public async Task LogScheduledOrderRequestAsync(
            string scheduleId,
            string ticker,
            string exchange,
            string action,
            int quantity,
            string accountAlias,
            DateTime? nextExecution)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(accountAlias);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildScheduledOrderRequestMessage(
                    scheduleId, ticker, exchange, action, quantity, accountAlias, nextExecution);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send scheduled order request log to Discord");
            }
        }

        public async Task LogScheduledOrderSuccessAsync(
            string scheduleId,
            string ticker,
            string exchange,
            string action,
            int quantity,
            string accountAlias)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(accountAlias);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildScheduledOrderSuccessMessage(
                    scheduleId, ticker, exchange, action, quantity, accountAlias);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send scheduled order success log to Discord");
            }
        }

        public async Task LogScheduledOrderFailureAsync(
            string scheduleId,
            string ticker,
            string exchange,
            string action,
            int quantity,
            string accountAlias,
            string reason)
        {
            if (!_config.Enabled) return;

            try
            {
                string? webhookUrl = GetAccountWebhookUrl(accountAlias);
                if (string.IsNullOrEmpty(webhookUrl)) return;

                var payload = _messageBuilder.BuildScheduledOrderFailureMessage(
                    scheduleId, ticker, exchange, action, quantity, accountAlias, reason);
                await SendAsync(webhookUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send scheduled order failure log to Discord");
            }
        }

        private string? GetSignalWebhookUrl(Signal signal)
        {
            // 1. Check Strategy mapping
            if (_config.Channels.Signals != null && _config.Channels.Signals.TryGetValue(signal.Strategy, out var strategyUrl))
            {
                return strategyUrl;
            }

            // 2. General Signal
            if (_config.Channels.Signals != null && _config.Channels.Signals.TryGetValue("General", out var generalUrl))
            {
                return generalUrl;
            }

            return _config.Channels.Default;
        }

        private string? GetAccountWebhookUrl(string accountAlias)
        {
            if (_config.Channels.Accounts != null && _config.Channels.Accounts.TryGetValue(accountAlias, out var url))
            {
                if (!string.IsNullOrEmpty(url)) return url;
            }

            return _config.Channels.Default;
        }

        private async Task SendAsync(string url, DiscordWebhookPayload payload)
        {
            if (string.IsNullOrEmpty(url)) return;

            // Fire-and-forget wrapper if needed, but here we just await the HTTP call.
            // The caller is responsible for not awaiting this method if they want fire-and-forget,
            // OR we can wrap it here.
            // The spec says: "logSignal() 호출 (단, Await 하지 않음)" in Webhook app.
            // But also "비동기 처리 정책: 모든 메서드는 async로 구현하되, 호출부에서 Wait()를 하지 않도록 설계 가이드 제공".
            // So I will just await the HTTP call here. The try-catch block handles exceptions so it won't crash the caller.

            using var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();
        }
    }
}
