using System.Collections.Generic;
using yQuant.Core.Models;
using yQuant.Infra.Notification.Common.Services;

namespace yQuant.Infra.Notification.Telegram
{
    public class TelegramMessageBuilder
    {
        private readonly TemplateService _templateService;

        public TelegramMessageBuilder(TemplateService templateService)
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
            return _templateService.ProcessTelegramTemplate("OrderSuccess", values);
        }

        public string BuildOrderFailureMessage(Order order, string message)
        {
            var values = new Dictionary<string, string>
            {
                { "Ticker", order.Ticker },
                { "Message", message }
            };
            return _templateService.ProcessTelegramTemplate("OrderFailure", values);
        }

        public string BuildAccountSyncFailureMessage(string alias, string message)
        {
            var values = new Dictionary<string, string>
            {
                { "Alias", alias },
                { "Message", message }
            };
            return _templateService.ProcessTelegramTemplate("AccountSyncFailure", values);
        }

        public string BuildNoAccountConfigMessage(string alias, string ticker)
        {
            var values = new Dictionary<string, string>
            {
                { "Alias", alias },
                { "Ticker", ticker }
            };
            return _templateService.ProcessTelegramTemplate("NoAccountConfig", values);
        }

        public string BuildNoBrokerAdapterMessage(string alias, string ticker)
        {
            var values = new Dictionary<string, string>
            {
                { "Alias", alias },
                { "Ticker", ticker }
            };
            return _templateService.ProcessTelegramTemplate("NoBrokerAdapter", values);
        }
    }
}
