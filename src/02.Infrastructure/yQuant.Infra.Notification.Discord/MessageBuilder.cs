using System;
using System.Collections.Generic;
using yQuant.Core.Models;
using yQuant.Infra.Notification.Common.Models;
using yQuant.Infra.Notification.Common.Services;

namespace yQuant.Infra.Notification.Discord
{
    public class MessageBuilder
    {
        private readonly TemplateService _templateService;

        public MessageBuilder(TemplateService templateService)
        {
            _templateService = templateService;
        }

        public DiscordWebhookPayload BuildSignalMessage(Signal signal, string timeframe)
        {
            var values = new Dictionary<string, string>
            {
                { "Source", signal.Source },
                { "Ticker", signal.Ticker },
                { "Action", signal.Action.ToString() },
                { "Price", signal.Price?.ToString("F2") ?? "Market" },
                { "Strength", signal.Strength?.ToString() ?? "N/A" },
                { "Timeframe", timeframe },
                { "Timestamp", signal.Timestamp.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate("Signal", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }

        public DiscordWebhookPayload BuildExecutionMessage(Order order)
        {
            var templateName = order.Action == OrderAction.Buy ? "Execution_Buy" : "Execution_Sell";
            var values = new Dictionary<string, string>
            {
                { "Action", order.Action.ToString() },
                { "Ticker", order.Ticker },
                { "Qty", order.Qty.ToString() },
                { "Price", order.Price?.ToString("F2") ?? "Market" },
                { "Type", order.Type.ToString() },
                { "AccountAlias", order.AccountAlias },
                { "Timestamp", order.Timestamp.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate(templateName, values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }

        public DiscordWebhookPayload BuildOrderFailureMessage(Order order, string reason)
        {
            var values = new Dictionary<string, string>
            {
                { "Action", order.Action.ToString() },
                { "Ticker", order.Ticker },
                { "Qty", order.Qty.ToString() },
                { "Price", order.Price?.ToString("F2") ?? "Market" },
                { "Type", order.Type.ToString() },
                { "AccountAlias", order.AccountAlias },
                { "Reason", reason },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate("Execution_Failure", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }

        public DiscordWebhookPayload BuildErrorMessage(string title, Exception ex, string context)
        {
            var stackTrace = ex.StackTrace ?? "";
            if (stackTrace.Length > 1000)
            {
                stackTrace = stackTrace.Substring(0, 1000) + "...";
            }

            var values = new Dictionary<string, string>
            {
                { "Title", title },
                { "Context", context },
                { "Message", ex.Message },
                { "StackTrace", stackTrace },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate("Error", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }

        public DiscordWebhookPayload BuildSummaryMessage(string accountAlias, PerformanceLog summary)
        {
            var values = new Dictionary<string, string>
            {
                { "AccountAlias", accountAlias },
                { "Date", summary.Date.ToString("yyyy-MM-dd") },
                { "TotalEquity", $"{summary.TotalEquity:N2} {summary.Currency}" },
                { "DailyPnL", $"{summary.DailyPnL:N2} {summary.Currency}" },
                { "DailyReturn", $"{summary.DailyReturn:P2}" },
                { "PositionsCount", summary.PositionsCount.ToString() },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate("Summary", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }

        public DiscordWebhookPayload BuildStartupMessage(string appName, string version)
        {
            var values = new Dictionary<string, string>
            {
                { "AppName", appName },
                { "Version", version },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate("Startup", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }

        public DiscordWebhookPayload BuildStatusMessage(string context, string message)
        {
            var values = new Dictionary<string, string>
            {
                { "Context", context },
                { "Message", message },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessDiscordTemplate("Status", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed } };
        }
    }
}
