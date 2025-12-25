using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;
using yQuant.Core.Models;
using yQuant.Core.Policies;

namespace yQuant.Core.Tests;

public class ConfigVerificationTests
{
    private readonly IConfiguration _config;

    public ConfigVerificationTests()
    {
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        _config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsecrets.json", optional: false)
            .Build();
    }

    [Fact]
    public void AppSettings_MarketSections_Exist()
    {
        var markets = _config.GetSection("Markets").GetChildren();
        Assert.NotEmpty(markets);

        foreach (var market in markets)
        {
            Assert.NotNull(market["TimeZone"]);
            Assert.NotNull(market["Currency"]);
            Assert.NotNull(market.GetSection("Exchanges").Get<string[]>());
            Assert.NotNull(market.GetSection("TradingHours"));
        }
    }

    [Fact]
    public void AppSettings_OrderManagerPolicies_Exist()
    {
        var sizing = _config.GetSection("OrderManager:Policies:Sizing:Basic");
        Assert.True(sizing.Exists());
        Assert.Equal(0.01m, sizing.GetValue<decimal>("MaxPositionRiskPct"));
        Assert.Equal(10000m, sizing.GetValue<decimal>("MinOrderAmt"));
    }

    [Fact]
    public void AppSecrets_WebhookStrategyAccountMapping_Exist()
    {
        var strategies = _config.GetSection("Webhook:StrategyAccountMapping");
        Assert.True(strategies.Exists());
        var accounts = strategies.GetSection("*").Get<string[]>();
        Assert.NotNull(accounts);
        Assert.Contains("Trading", accounts);
    }

    [Fact]
    public void AppSecrets_BrokerGatewayAccounts_Exist()
    {
        var accounts = _config.GetSection("BrokerGateway:Accounts");
        Assert.True(accounts.Exists());

        var trading = accounts.GetSection("Trading");
        Assert.True(trading.Exists());
        Assert.Equal("KIS", trading["Broker"]);
        Assert.NotNull(trading["AppKey"]);
        Assert.NotNull(trading["AppSecret"]);
    }

    [Fact]
    public void AppSecrets_Notifier_Exist()
    {
        var notifier = _config.GetSection("Notifier");
        Assert.True(notifier.Exists());

        var discord = notifier.GetSection("Discord");
        Assert.True(discord.Exists());
        var channels = discord.GetSection("Channels");
        Assert.True(channels.Exists());
        Assert.NotNull(channels["Default"]);

        var telegram = notifier.GetSection("Telegram");
        Assert.True(telegram.Exists());
        Assert.NotNull(telegram["BotToken"]);
        Assert.NotNull(telegram["ChatId"]);
    }

    [Fact]
    public void AppSecrets_Web_Users_Exist()
    {
        var users = _config.GetSection("Web:Users");
        Assert.True(users.Exists());

        var userY = users.GetSection("y");
        Assert.True(userY.Exists());
        Assert.NotNull(userY["PasswordHash"]);
    }

    [Fact]
    public void AppSecrets_Webhook_Secret_Exists()
    {
        var secret = _config["Webhook:Secrets:TradingView"];
        Assert.False(string.IsNullOrEmpty(secret));
    }

    [Fact]
    public void AppSecrets_OrderManagerStrategySizingMapping_Exists()
    {
        var mapping = _config.GetSection("OrderManager:StrategySizingMapping");
        Assert.True(mapping.Exists());
        var value = mapping["*"];
        Assert.True(value == "Basic" || value == "OnlyOne", $"Expected 'Basic' or 'OnlyOne', but got '{value}'");
    }
}
