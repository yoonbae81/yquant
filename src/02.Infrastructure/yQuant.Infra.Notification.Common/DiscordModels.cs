using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace yQuant.Infra.Notification.Common.Models
{
    public class DiscordWebhookPayload
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("embeds")]
        public List<DiscordEmbed> Embeds { get; set; }
    }

    public class DiscordEmbed
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("color")]
        public int? Color { get; set; }

        [JsonPropertyName("fields")]
        public List<DiscordField> Fields { get; set; }

        [JsonPropertyName("footer")]
        public DiscordFooter Footer { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }
    }

    public class DiscordField
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("inline")]
        public bool? Inline { get; set; }
    }

    public class DiscordFooter
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }
}
