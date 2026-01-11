using System.Collections.Generic;

namespace yQuant.Infra.Notification.Discord
{
    public class DiscordConfiguration
    {
        public bool Enabled { get; set; }
        public int TimeoutMs { get; set; } = 2000;
        public DiscordChannelsConfiguration Channels { get; set; } = new();
    }

    public class DiscordChannelsConfiguration
    {
        public string? Default { get; set; }
        public DiscordSystemChannels? System { get; set; }
        public Dictionary<string, string>? Signals { get; set; }
        public Dictionary<string, string>? Accounts { get; set; }
    }

    public class DiscordSystemChannels
    {
        public string? Status { get; set; }
        public string? Security { get; set; }
        public string? Error { get; set; }
        public string? Catalog { get; set; }
    }
}
