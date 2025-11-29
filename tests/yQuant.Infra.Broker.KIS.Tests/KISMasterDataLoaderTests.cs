using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using yQuant.Core.Models;
using yQuant.Infra.Master.KIS;

namespace yQuant.Infra.Broker.KIS.Tests;

public class KISMasterDataLoaderTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<KISMasterDataLoader>> _mockLogger;
    private readonly KISMasterDataLoader _loader;

    public KISMasterDataLoaderTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<KISMasterDataLoader>>();
        _loader = new KISMasterDataLoader(_mockHttpClientFactory.Object, _mockLogger.Object);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Theory]
    [InlineData("SSE", CurrencyType.CNY)]
    [InlineData("SZSE", CurrencyType.CNY)]
    [InlineData("TSE", CurrencyType.JPY)]
    [InlineData("HKEX", CurrencyType.HKD)]
    [InlineData("HNX", CurrencyType.VND)]
    [InlineData("HOSE", CurrencyType.VND)]
    [InlineData("NASDAQ", CurrencyType.USD)]
    [InlineData("NYSE", CurrencyType.USD)]
    [InlineData("AMEX", CurrencyType.USD)]
    public async Task LoadMasterDataAsync_ShouldAssignCorrectCurrency_ForOverseasExchanges(string exchange, CurrencyType expectedCurrency)
    {
        // Arrange
        var mockContent = "0000\t0000\t0000\t0000\tTICKER\t0000\tKOR_NAME\tENG_NAME\t0000"; // Mock overseas format
        var zipStream = CreateZipStream(mockContent);
        
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(zipStream.ToArray())
            });

        var client = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(client);

        // Act
        var result = await _loader.LoadMasterDataAsync(exchange, "http://test.com/master.zip");

        // Assert
        Assert.Single(result);
        var stock = result.First();
        Assert.Equal(exchange, stock.Exchange);
        Assert.Equal(expectedCurrency, stock.Currency);
    }

    private MemoryStream CreateZipStream(string content)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("master.txt");
            using (var entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, Encoding.GetEncoding("euc-kr"))) // KIS uses EUC-KR
            {
                writer.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }
}
