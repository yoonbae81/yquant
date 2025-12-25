using System.Collections.Generic;
using yQuant.Core.Models;
using yQuant.Infra.Notification.Telegram.Services;

namespace yQuant.Infra.Notification.Telegram
{
    public class TelegramMessageBuilder
    {
        private readonly TelegramTemplateService _templateService;

        public TelegramMessageBuilder(TelegramTemplateService templateService)
        {
            _templateService = templateService;
        }

        public string BuildOrderSuccessMessage(Order order)
        {
            var values = new Dictionary<string, string>
            {
                { "Ticker", order.Ticker },
                { "Action", order.Action.ToString() },
                { "Qty", order.Qty.ToString() },
                { "Price", order.Price?.ToString() ?? "Market" }
            };
            return _templateService.ProcessTemplate("OrderSuccess", values) ?? string.Empty;
        }

        public string BuildOrderFailureMessage(Order order, string message)
        {
            var values = new Dictionary<string, string>
            {
                { "Ticker", order.Ticker },
                { "Message", message }
            };
            return _templateService.ProcessTemplate("OrderFailure", values) ?? string.Empty;
        }

        public string BuildAccountSyncFailureMessage(string alias, string message)
        {
            var values = new Dictionary<string, string>
            {
                { "Alias", alias },
                { "Message", message }
            };
            return _templateService.ProcessTemplate("AccountSyncFailure", values) ?? string.Empty;
        }

        public string BuildNoAccountConfigMessage(string alias, string ticker)
        {
            var values = new Dictionary<string, string>
            {
                { "Alias", alias },
                { "Ticker", ticker }
            };
            return _templateService.ProcessTemplate("NoAccountConfig", values) ?? string.Empty;
        }

        public string BuildNoBrokerAdapterMessage(string alias, string ticker)
        {
            var values = new Dictionary<string, string>
            {
                { "Alias", alias },
                { "Ticker", ticker }
            };
            return _templateService.ProcessTemplate("NoBrokerAdapter", values) ?? string.Empty;
        }
    }
}
