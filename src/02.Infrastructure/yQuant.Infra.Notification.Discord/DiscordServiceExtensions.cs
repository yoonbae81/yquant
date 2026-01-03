using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Notification.Discord.Services;

namespace yQuant.Infra.Notification.Discord;

public static class DiscordServiceExtensions
{
    public static IHostApplicationBuilder AddDiscordDirectNotification(this IHostApplicationBuilder builder, string sectionPath = "Notifier:Discord")
    {
        var discordConfig = builder.Configuration.GetSection(sectionPath);

        // Configuration
        builder.Services.Configure<DiscordConfiguration>(discordConfig);

        // HTTP Client
        builder.Services.AddHttpClient("DiscordWebhook");

        // Services
        builder.Services.AddSingleton<DiscordTemplateService>();
        builder.Services.AddSingleton<DiscordLogger>();

        // Interface registration
        builder.Services.AddSingleton<ISystemLogger>(sp => sp.GetRequiredService<DiscordLogger>());

        return builder;
    }
}
