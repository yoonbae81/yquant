using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Master.KIS;

public class KISMasterDataLoader : IMasterDataLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KISMasterDataLoader> _logger;

    public KISMasterDataLoader(IHttpClientFactory httpClientFactory, ILogger<KISMasterDataLoader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<StockMaster>> LoadMasterDataAsync(string exchange, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("URL for {Exchange} is empty.", exchange);
            return Enumerable.Empty<StockMaster>();
        }

        _logger.LogInformation("Downloading {Exchange} Master File from {Url}", exchange, url);
        var content = await DownloadAndUnzipAsync(url);
        if (content == null) return Enumerable.Empty<StockMaster>();

        return ParseMasterData(exchange, content);
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
            var entry = archive.Entries.FirstOrDefault(); // Assuming single file in zip
            if (entry == null) return null;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or unzip master file from {Url}", url);
            return null;
        }
    }

    private IEnumerable<StockMaster> ParseMasterData(string exchange, byte[] content)
    {
        // Register encoding provider for CP949
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding("EUC-KR"); // CP949

        var text = encoding.GetString(content);
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<StockMaster>();

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
            return Enumerable.Empty<StockMaster>();
        }
    }

    private IEnumerable<StockMaster> ParseKospi(string[] lines)
    {
        var list = new List<StockMaster>();
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

            list.Add(new StockMaster
            {
                Ticker = ticker,
                Name = name,
                Exchange = "KOSPI",
                Currency = CurrencyType.KRW
            });
        }
        return list;
    }

    private IEnumerable<StockMaster> ParseKosdaq(string[] lines)
    {
        var list = new List<StockMaster>();
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

            list.Add(new StockMaster
            {
                Ticker = ticker,
                Name = name,
                Exchange = "KOSDAQ",
                Currency = CurrencyType.KRW
            });
        }
        return list;
    }

    private IEnumerable<StockMaster> ParseOverseas(string[] lines, string exchange)
    {
        var list = new List<StockMaster>();
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length < 7) continue;

            // Column 4: Symbol (Ticker)
            // Column 7: English Name (Index 7) - Check if this is correct based on python code
            // Python code: columns = [..., 'Symbol', 'realtime symbol', 'Korea name', 'English name', ...]
            // Index: 0, 1, 2, 3, 4(Symbol), 5, 6(Korea), 7(English)
            
            var ticker = parts[4].Trim();
            var name = parts[7].Trim(); 
            if (string.IsNullOrEmpty(name)) name = parts[6].Trim(); // Fallback to Korean name

            list.Add(new StockMaster
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
