using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace yQuant.Infra.Notification.Telegram.Services
{
    public class TelegramTemplateService
    {
        private readonly string _templateDirectory;
        private Dictionary<string, string> _telegramTemplates = new();

        public TelegramTemplateService(string? templateDirectory = null)
        {
            _templateDirectory = templateDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            var telegramPath = Path.Combine(_templateDirectory, "telegram-templates.json");
            if (File.Exists(telegramPath))
            {
                var json = File.ReadAllText(telegramPath);
                _telegramTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            else
            {
                _telegramTemplates = new Dictionary<string, string>();
            }
        }

        public string? GetTemplate(string templateName)
        {
            return _telegramTemplates.TryGetValue(templateName, out var template) ? template : null;
        }

        public string? ProcessTemplate(string templateName, Dictionary<string, string> values)
        {
            var template = GetTemplate(templateName);
            if (template == null) return null;

            return ReplaceValues(template, values);
        }

        private string ReplaceValues(string text, Dictionary<string, string> values)
        {
            foreach (var kvp in values)
            {
                text = text.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
            return text;
        }
    }
}
