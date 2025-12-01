using System;
using System.Linq;
using Xunit;
using yQuant.Core.Models;
using yQuant.Infra.Notification.Discord;
using yQuant.Infra.Notification.Discord.Models;
using yQuant.Infra.Notification.Discord.Services;
using System.IO;

namespace yQuant.Infra.Notification.Discord.Tests
{
    public class MessageBuilderTests
    {
        private readonly MessageBuilder _builder;

        public MessageBuilderTests()
        {
            var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            var templateService = new DiscordTemplateService(templateDir);
            _builder = new MessageBuilder(templateService);
        }

        [Fact]
        public void BuildSignalMessage_ShouldCreateCorrectEmbed()
        {
            // Arrange
            var signal = new Signal
            {
                Ticker = "AAPL",
                Exchange = "NASDAQ",
                Action = OrderAction.Buy,
                Price = 150.00m,
                Strength = 80,
                Source = "TrendFollow_A",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var payload = _builder.BuildSignalMessage(signal, "1h");

            // Assert
            Assert.Single(payload.Embeds!);
            var embed = payload.Embeds!.First();
            Assert.Equal("Signal: TrendFollow_A", embed.Title);
            Assert.Equal(3447003, embed.Color); // Blue
            Assert.Equal("Timeframe: 1h", embed.Footer!.Text);

            Assert.Contains(embed.Fields!, f => f.Name == "Ticker" && f.Value == "AAPL");
            Assert.Contains(embed.Fields!, f => f.Name == "Action" && f.Value == "Buy");
            Assert.Contains(embed.Fields!, f => f.Name == "Price" && f.Value == "150.00");
            Assert.Contains(embed.Fields!, f => f.Name == "Strength" && f.Value == "80");
        }

        [Fact]
        public void BuildExecutionMessage_Buy_ShouldBeGreen()
        {
            // Arrange
            var order = new Order
            {
                AccountAlias = "test_account",
                Ticker = "TSLA",
                Action = OrderAction.Buy,
                Type = OrderType.Limit,
                Qty = 10,
                Price = 200.00m,
                Timestamp = DateTime.UtcNow
            };

            // Act
            var payload = _builder.BuildExecutionMessage(order);

            // Assert
            var embed = payload.Embeds!.First();
            Assert.Equal("Order Executed: Buy", embed.Title);
            Assert.Equal(5763719, embed.Color); // Green
            Assert.Contains(embed.Fields!, f => f.Name == "Ticker" && f.Value == "TSLA");
        }

        [Fact]
        public void BuildExecutionMessage_Sell_ShouldBeRed()
        {
            // Arrange
            var order = new Order
            {
                AccountAlias = "test_account",
                Ticker = "TSLA",
                Action = OrderAction.Sell,
                Type = OrderType.Market,
                Qty = 5,
                Timestamp = DateTime.UtcNow
            };

            // Act
            var payload = _builder.BuildExecutionMessage(order);

            // Assert
            var embed = payload.Embeds!.First();
            Assert.Equal("Order Executed: Sell", embed.Title);
            Assert.Equal(15548997, embed.Color); // Red
        }

        [Fact]
        public void BuildOrderFailureMessage_ShouldBeRedAndContainReason()
        {
            // Arrange
            var order = new Order
            {
                AccountAlias = "test_account",
                Ticker = "TSLA",
                Action = OrderAction.Buy,
                Type = OrderType.Limit,
                Qty = 10,
                Price = 200.00m,
                Timestamp = DateTime.UtcNow
            };
            var reason = "Insufficient Funds";

            // Act
            var payload = _builder.BuildOrderFailureMessage(order, reason);

            // Assert
            var embed = payload.Embeds!.First();
            Assert.Equal("Order Rejected", embed.Title);
            Assert.Equal(15548997, embed.Color); // Red
            Assert.Contains(embed.Fields!, f => f.Name == "Ticker" && f.Value == "TSLA");
            Assert.Contains("Insufficient Funds", embed.Description);
        }

        [Fact]
        public void BuildErrorMessage_ShouldTruncateStackTrace()
        {
            // Arrange
            var ex = new Exception("Test Error");
            try { throw ex; } catch (Exception e) { ex = e; }

            // Act
            var payload = _builder.BuildErrorMessage("Test Error Title", ex, "TestContext");

            // Assert
            var embed = payload.Embeds!.First();
            Assert.Equal("Test Error Title", embed.Title);
            Assert.Contains("TestContext", embed.Description);
            Assert.Contains("Test Error", embed.Description);
            Assert.Equal(15105570, embed.Color); // Orange
        }
    }
}
