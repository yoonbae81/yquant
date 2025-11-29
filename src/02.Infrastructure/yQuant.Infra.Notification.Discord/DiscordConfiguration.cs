using System.Collections.Generic;

namespace yQuant.Infra.Notification.Discord
{
    public class DiscordConfiguration
    {
        public bool Enabled { get; set; }
        public string DefaultWebhookUrl { get; set; }
        public int TimeoutMs { get; set; } = 2000;
        public DiscordSystemConfiguration System { get; set; }
        public Dictionary<string, string> Signal { get; set; }
        public Dictionary<string, string> Accounts { get; set; }
    }

    public class DiscordSystemConfiguration
    {
        public string Status { get; set; }
        public string Error { get; set; }
    }
}
