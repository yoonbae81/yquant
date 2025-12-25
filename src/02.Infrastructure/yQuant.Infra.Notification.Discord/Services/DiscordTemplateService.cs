using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using yQuant.Infra.Notification.Discord.Models;

namespace yQuant.Infra.Notification.Discord.Services
{
    public class DiscordTemplateService
    {
        private readonly string _templateDirectory;
        private Dictionary<string, DiscordEmbed> _discordTemplates = new();

        public DiscordTemplateService(string? templateDirectory = null)
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
                _discordTemplates = JsonSerializer.Deserialize<Dictionary<string, DiscordEmbed>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Dictionary<string, DiscordEmbed>();
            }
            else
            {
                _discordTemplates = new Dictionary<string, DiscordEmbed>();
            }
        }

        public DiscordEmbed? GetTemplate(string templateName)
        {
            return _discordTemplates.TryGetValue(templateName, out var template) ? Clone(template) : null;
        }

        public DiscordEmbed? ProcessTemplate(string templateName, Dictionary<string, string> values)
        {
            var template = GetTemplate(templateName);
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

            return template;
        }

        private string ReplaceValues(string text, Dictionary<string, string> values)
        {
            foreach (var kvp in values)
            {
                text = text.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
            return text;
        }

        private DiscordEmbed? Clone(DiscordEmbed source)
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<DiscordEmbed>(json);
        }
    }
}
