using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using yQuant.Infra.Notification.Common.Models;

namespace yQuant.Infra.Notification.Common.Services
{
    public class TemplateService
    {
        private readonly string _templateDirectory;
        private Dictionary<string, DiscordEmbed> _discordTemplates;
        private Dictionary<string, string> _telegramTemplates;

        public TemplateService(string templateDirectory = null)
        {
            _templateDirectory = templateDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            var discordPath = Path.Combine(_templateDirectory, "discord-templates.json");
            if (File.Exists(discordPath))
            {
                var json = File.ReadAllText(discordPath);
                _discordTemplates = JsonSerializer.Deserialize<Dictionary<string, DiscordEmbed>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else
            {
                _discordTemplates = new Dictionary<string, DiscordEmbed>();
            }

            var telegramPath = Path.Combine(_templateDirectory, "telegram-templates.json");
            if (File.Exists(telegramPath))
            {
                var json = File.ReadAllText(telegramPath);
                _telegramTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            else
            {
                _telegramTemplates = new Dictionary<string, string>();
            }
        }

        public DiscordEmbed GetDiscordTemplate(string templateName)
        {
            return _discordTemplates.TryGetValue(templateName, out var template) ? Clone(template) : null;
        }

        public string GetTelegramTemplate(string templateName)
        {
            return _telegramTemplates.TryGetValue(templateName, out var template) ? template : null;
        }

        public DiscordEmbed ProcessDiscordTemplate(string templateName, Dictionary<string, string> values)
        {
            var template = GetDiscordTemplate(templateName);
            if (template == null) return null;

            if (template.Title != null) template.Title = ReplaceValues(template.Title, values);
            if (template.Description != null) template.Description = ReplaceValues(template.Description, values);
            if (template.Url != null) template.Url = ReplaceValues(template.Url, values);
            if (template.Footer != null)
            {
                if (template.Footer.Text != null) template.Footer.Text = ReplaceValues(template.Footer.Text, values);
                if (template.Footer.IconUrl != null) template.Footer.IconUrl = ReplaceValues(template.Footer.IconUrl, values);
            }
            if (template.Timestamp != null) template.Timestamp = ReplaceValues(template.Timestamp, values);

            if (template.Fields != null)
            {
                foreach (var field in template.Fields)
                {
                    if (field.Name != null) field.Name = ReplaceValues(field.Name, values);
                    if (field.Value != null) field.Value = ReplaceValues(field.Value, values);
                }
            }
            
            // Handle Color if it's a placeholder (not supported by int?, but maybe we want to support it?)
            // The template has "color": 3447003. If we want dynamic color, we need to handle it.
            // Current JSON has integer. If we want dynamic, we'd need string in JSON and parse it.
            // For now, let's assume color is static in template or handled by caller if needed.
            // But wait, Execution template has dynamic color based on Buy/Sell.
            // I created Execution_Buy and Execution_Sell, so color is static in each.
            
            return template;
        }

        public string ProcessTelegramTemplate(string templateName, Dictionary<string, string> values)
        {
            var template = GetTelegramTemplate(templateName);
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

        private DiscordEmbed Clone(DiscordEmbed source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<DiscordEmbed>(json);
        }
    }
}
