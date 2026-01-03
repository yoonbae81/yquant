using System;
using System.Collections.Generic;
using yQuant.Core.Models;
using yQuant.Infra.Notification.Discord.Models;
using yQuant.Infra.Notification.Discord.Services;

namespace yQuant.Infra.Notification.Discord
{
    public class MessageBuilder
    {
        private readonly DiscordTemplateService _templateService;

        public MessageBuilder(DiscordTemplateService templateService)
        {
            _templateService = templateService;
        }

        public DiscordWebhookPayload BuildSignalMessage(Signal signal, string timeframe)
        {
            var values = new Dictionary<string, string>
            {
                { "Source", signal.Strategy },
                { "Ticker", signal.Ticker },
                { "Action", signal.Action.ToString() },
                { "Price", signal.Price?.ToString("F2") ?? "Market" },
                { "Strength", signal.Strength?.ToString() ?? "N/A" },
                { "Timeframe", timeframe },
                { "Timestamp", signal.Timestamp.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("Signal", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
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

            var embed = _templateService.ProcessTemplate(templateName, values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
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

            var embed = _templateService.ProcessTemplate("Execution_Failure", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }

        public DiscordWebhookPayload BuildErrorMessage(string title, Exception ex, string context, string host)
        {
            var stackTrace = ex.StackTrace ?? "";
            if (stackTrace.Length > 1000)
            {
                stackTrace = stackTrace.Substring(0, 1000) + "...";
            }

            var values = new Dictionary<string, string>
            {
                { "Host", host },
                { "Title", title },
                { "Context", context },
                { "Message", ex.Message },
                { "StackTrace", stackTrace },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("Error", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
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

            var embed = _templateService.ProcessTemplate("Summary", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }

        public DiscordWebhookPayload BuildStartupMessage(string appName, string host)
        {
            var values = new Dictionary<string, string>
            {
                { "AppName", appName },
                { "Host", host },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("Startup", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }

        public DiscordWebhookPayload BuildStatusMessage(string context, string message, string host)
        {
            var values = new Dictionary<string, string>
            {
                { "Host", host },
                { "Context", context },
                { "Message", message },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("Status", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }

        public DiscordWebhookPayload BuildScheduledOrderRequestMessage(
            string scheduleId,
            string ticker,
            string exchange,
            string action,
            int quantity,
            string accountAlias,
            DateTime? nextExecution)
        {
            var values = new Dictionary<string, string>
            {
                { "ScheduleId", scheduleId },
                { "Ticker", ticker },
                { "Exchange", exchange },
                { "Action", action },
                { "Quantity", quantity.ToString() },
                { "AccountAlias", accountAlias },
                { "NextExecution", nextExecution?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "N/A" },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("ScheduledOrder_Request", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }

        public DiscordWebhookPayload BuildScheduledOrderSuccessMessage(
            string scheduleId,
            string ticker,
            string exchange,
            string action,
            int quantity,
            string accountAlias)
        {
            var values = new Dictionary<string, string>
            {
                { "ScheduleId", scheduleId },
                { "Ticker", ticker },
                { "Exchange", exchange },
                { "Action", action },
                { "Quantity", quantity.ToString() },
                { "AccountAlias", accountAlias },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("ScheduledOrder_Success", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }

        public DiscordWebhookPayload BuildScheduledOrderFailureMessage(
            string scheduleId,
            string ticker,
            string exchange,
            string action,
            int quantity,
            string accountAlias,
            string reason)
        {
            var values = new Dictionary<string, string>
            {
                { "ScheduleId", scheduleId },
                { "Ticker", ticker },
                { "Exchange", exchange },
                { "Action", action },
                { "Quantity", quantity.ToString() },
                { "AccountAlias", accountAlias },
                { "Reason", reason },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            };

            var embed = _templateService.ProcessTemplate("ScheduledOrder_Failure", values);
            return new DiscordWebhookPayload { Embeds = new List<DiscordEmbed> { embed! } };
        }
    }
}
