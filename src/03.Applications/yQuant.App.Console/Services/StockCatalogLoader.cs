using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;

namespace yQuant.App.Console.Services;

/// <summary>
/// Loads stock catalog data from KIS (Korea Investment & Securities) master data files.
/// </summary>
public class StockCatalogLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StockCatalogLoader> _logger;

    public StockCatalogLoader(IHttpClientFactory httpClientFactory, ILogger<StockCatalogLoader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<Stock>> LoadCatalogAsync(string exchange, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("URL for {Exchange} is empty.", exchange);
            return Enumerable.Empty<Stock>();
        }

        _logger.LogInformation("Downloading {Exchange} catalog file from {Url}", exchange, url);
        var content = await DownloadAndUnzipAsync(url);
        if (content == null) return Enumerable.Empty<Stock>();

        return ParseCatalogData(exchange, content);
    }

    private async Task<byte[]?> DownloadAndUnzipAsync(string url)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault();
            if (entry == null) return null;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or unzip catalog file from {Url}", url);
            return null;
        }
    }

    private IEnumerable<Stock> ParseCatalogData(string exchange, byte[] content)
    {
        // Register encoding provider for CP949
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding("EUC-KR"); // CP949

        var text = encoding.GetString(content);
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (exchange.Equals("KOSPI", StringComparison.OrdinalIgnoreCase))
        {
            return ParseKospi(lines);
        }
        else if (exchange.Equals("KOSDAQ", StringComparison.OrdinalIgnoreCase))
        {
            return ParseKosdaq(lines);
        }
        else if (exchange.Equals("NASDAQ", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("NYSE", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("AMEX", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("SSE", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("SZSE", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("TSE", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("HKEX", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("HNX", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("HOSE", StringComparison.OrdinalIgnoreCase))
        {
            return ParseOverseas(lines, exchange);
        }
        else
        {
            _logger.LogWarning("Unknown exchange type: {Exchange}", exchange);
            return Enumerable.Empty<Stock>();
        }
    }

    private IEnumerable<Stock> ParseKospi(string[] lines)
    {
        var list = new List<Stock>();
        foreach (var line in lines)
        {
            // KOSPI Format: Part1 (len - 228) + Part2 (228)
            // Part1: Ticker(0-9), StandardCode(9-21), Name(21-end)
            if (line.Length <= 228) continue;

            var part1Len = line.Length - 228;
            var part1 = line.Substring(0, part1Len);

            if (part1.Length < 21) continue;

            var ticker = part1.Substring(0, 9).Trim();
            var name = part1.Substring(21).Trim();

            list.Add(new Stock
            {
                Ticker = ticker,
                Name = name,
                Exchange = "KOSPI",
                Currency = CurrencyType.KRW
            });
        }
        return list;
    }

    private IEnumerable<Stock> ParseKosdaq(string[] lines)
    {
        var list = new List<Stock>();
        foreach (var line in lines)
        {
            // KOSDAQ Format: Part1 (len - 222) + Part2 (222)
            // Part1: Ticker(0-9), StandardCode(9-21), Name(21-end)
            if (line.Length <= 222) continue;

            var part1Len = line.Length - 222;
            var part1 = line.Substring(0, part1Len);

            if (part1.Length < 21) continue;

            var ticker = part1.Substring(0, 9).Trim();
            var name = part1.Substring(21).Trim();

            list.Add(new Stock
            {
                Ticker = ticker,
                Name = name,
                Exchange = "KOSDAQ",
                Currency = CurrencyType.KRW
            });
        }
        return list;
    }

    private IEnumerable<Stock> ParseOverseas(string[] lines, string exchange)
    {
        var list = new List<Stock>();
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length < 7) continue;

            var ticker = parts[4].Trim();
            var name = parts[7].Trim();
            if (string.IsNullOrEmpty(name)) name = parts[6].Trim(); // Fallback to Korean name

            list.Add(new Stock
            {
                Ticker = ticker,
                Name = name,
                Exchange = exchange.ToUpper(),
                Currency = GetCurrencyForExchange(exchange)
            });
        }
        return list;
    }

    private CurrencyType GetCurrencyForExchange(string exchange)
    {
        if (exchange.Equals("SSE", StringComparison.OrdinalIgnoreCase) ||
            exchange.Equals("SZSE", StringComparison.OrdinalIgnoreCase))
        {
            return CurrencyType.CNY;
        }
        else if (exchange.Equals("TSE", StringComparison.OrdinalIgnoreCase))
        {
            return CurrencyType.JPY;
        }
        else if (exchange.Equals("HKEX", StringComparison.OrdinalIgnoreCase))
        {
            return CurrencyType.HKD;
        }
        else if (exchange.Equals("HNX", StringComparison.OrdinalIgnoreCase) ||
                 exchange.Equals("HOSE", StringComparison.OrdinalIgnoreCase))
        {
            return CurrencyType.VND;
        }
        else
        {
            return CurrencyType.USD; // Default to USD for NASDAQ, NYSE, AMEX
        }
    }
}
