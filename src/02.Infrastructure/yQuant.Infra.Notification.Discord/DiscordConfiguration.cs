using System.Collections.Generic;

namespace yQuant.Infra.Notification.Discord
{
    public class DiscordConfiguration
    {
        public bool Enabled { get; set; }
        public string DefaultWebhookUrl { get; set; }
        public int TimeoutMs { get; set; } = 2000;
        public DiscordSystemConfiguration System { get; set; }
        public DiscordSignalConfiguration Signal { get; set; }
        public Dictionary<string, DiscordAccountConfiguration> Accounts { get; set; }
    }

    public class DiscordSystemConfiguration
    {
        public string Status { get; set; }
        public string Error { get; set; }
    }

    public class DiscordSignalConfiguration
    {
        public Dictionary<string, string> Mappings { get; set; }
    }

    public class DiscordAccountConfiguration
    {
        public string Description { get; set; }
        public DiscordAccountChannels Channels { get; set; }
    }

    public class DiscordAccountChannels
    {
        public string Order { get; set; }
        public string Error { get; set; }
        public string Report { get; set; }
    }
}
