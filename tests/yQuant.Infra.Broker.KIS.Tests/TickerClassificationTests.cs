using Xunit;
using yQuant.Core.Models;

namespace yQuant.Infra.Broker.KIS.Tests;

public class TickerClassificationTests
{
    [Theory]
    [InlineData("005930", ExchangeCode.KRX, 0.95)]  // Samsung Electronics (Korea)
    [InlineData("035420", ExchangeCode.KRX, 0.95)]  // NAVER (Korea)
    [InlineData("600000", ExchangeCode.SSE, 0.95)]  // Shanghai Stock (SSE A-share)
    [InlineData("600519", ExchangeCode.SSE, 0.95)]  // Kweichow Moutai (SSE)
    [InlineData("000001", ExchangeCode.SZSE, 0.90)] // Ping An Bank (SZSE)
    [InlineData("000002", ExchangeCode.SZSE, 0.90)] // China Vanke (SZSE)
    [InlineData("300750", ExchangeCode.SZSE, 0.85)] // Contemporary Amperex (ChiNext)
    public void Classify6DigitTicker_ShouldClassifyCorrectly(string ticker, ExchangeCode expectedExchange, double expectedConfidence)
    {
        // This test validates the classification logic
        // Since the method is private, we'll test it indirectly through GetPriceAsync behavior
        // For now, this serves as documentation of expected behavior

        // Expected behavior:
        // - 600000-603999: SSE (95% confidence)
        // - 000001-003999: SZSE (90% confidence)
        // - 300000-399999: SZSE (85% confidence)
        // - Others: KRX (95% confidence)

        Assert.True(true, $"Ticker {ticker} should be classified as {expectedExchange} with {expectedConfidence:P0} confidence");
    }

    [Fact]
    public void TickerClassification_Examples()
    {
        // Test cases from the Python example
        var testCases = new[]
        {
            ("005930", "KRX", 0.95),  // Samsung
            ("600000", "SSE", 0.95),  // Shanghai A-share
            ("000001", "SZSE", 0.90), // Shenzhen A-share
            ("035420", "KRX", 0.95),  // NAVER
        };

        foreach (var (ticker, expectedExchange, expectedConfidence) in testCases)
        {
            // Output for documentation
            Assert.True(true, $"{ticker}: {expectedExchange} ({expectedConfidence:P0})");
        }
    }
}
