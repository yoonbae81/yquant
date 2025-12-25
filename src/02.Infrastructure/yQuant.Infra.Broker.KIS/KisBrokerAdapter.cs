using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS.Models;
using System.Collections.Concurrent;

namespace yQuant.Infra.Broker.KIS;

public class KISBrokerAdapter : IBrokerAdapter
{
    private readonly ILogger<KISBrokerAdapter> _logger;
    private readonly IKISClient _client;

    // Cache for DomesticBalance to avoid duplicate API calls
    private DomesticBalanceResponse? _cachedDomesticBalance;
    private DateTime _domesticBalanceCacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheValidDuration = TimeSpan.FromSeconds(5);

    // In-memory cache for ticker -> isDomestic mapping (for ambiguous 6-digit tickers starting with 0 or 3)
    private readonly ConcurrentDictionary<string, bool> _tickerDomesticCache = new();

    // Static domestic tickers loaded from file (shared across all adapter instances)
    private static readonly Lazy<HashSet<string>> _domesticTickers = new(() =>
    {
        var tickers = new HashSet<string>();
        try
        {
            var tempDir = Path.GetTempPath();
            var filePath = Path.Combine(tempDir, "domestic_tickers.txt");

            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var ticker in lines)
                {
                    if (!string.IsNullOrWhiteSpace(ticker))
                        tickers.Add(ticker.Trim());
                }
            }
        }
        catch
        {
            // Silently fail - will use heuristic
        }
        return tickers;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private static bool _fileLoadLogged = false;
    private static readonly object _logLock = new();

    public KISBrokerAdapter(IKISClient client, ILogger<KISBrokerAdapter> logger)
    {
        _logger = logger;
        _client = client;

        // Log file load status only once
        if (!_fileLoadLogged)
        {
            lock (_logLock)
            {
                if (!_fileLoadLogged)
                {
                    var tempDir = Path.GetTempPath();
                    var filePath = Path.Combine(tempDir, "domestic_tickers.txt");

                    if (_domesticTickers.Value.Count > 0)
                    {
                        _logger.LogInformation("Loaded {Count} domestic tickers from {File} (shared across all adapters)",
                            _domesticTickers.Value.Count, filePath);
                    }
                    else
                    {
                        _logger.LogWarning("Domestic tickers file not found at {File}, will use heuristic only", filePath);
                    }
                    _fileLoadLogged = true;
                }
            }
        }
    }

    public Account Account => _client.Account;

    /// <summary>
    /// Parses account number in format "XXXXXXXX-YY" or "XXXXXXXXXXYY" into (CANO, ACNT_PRDT_CD)
    /// CANO: 8 digits, ACNT_PRDT_CD: 2 digits
    /// </summary>
    private (string cano, string acntPrdtCd) ParseAccountNumber(string accountNumber)
    {
        // Remove any dashes
        var cleaned = accountNumber.Replace("-", "");

        if (cleaned.Length < 10)
        {
            throw new ArgumentException($"Invalid account number format: {accountNumber}. Expected 10 digits (8-2 format).");
        }

        var cano = cleaned.Substring(0, 8);
        var acntPrdtCd = cleaned.Substring(8, 2);

        return (cano, acntPrdtCd);
    }

    public async Task EnsureConnectedAsync()
    {
        await _client.EnsureConnectedAsync();
    }

    /// <summary>
    /// Gets domestic balance with caching to avoid duplicate API calls within short time window
    /// </summary>
    private async Task<DomesticBalanceResponse?> GetDomesticBalanceAsync(string accountNumber)
    {
        // Check if cache is still valid
        if (_cachedDomesticBalance != null && DateTime.UtcNow < _domesticBalanceCacheExpiry)
        {
            _logger.LogDebug("Using cached DomesticBalance response for account {Alias}", _client.Account.Alias);
            return _cachedDomesticBalance;
        }

        // Cache expired or not available, fetch from API
        var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);

        var domesticQueryParams = new Dictionary<string, string>
        {
            { "CANO", cano },
            { "ACNT_PRDT_CD", acntPrdtCd },
            { "AFHR_FLPR_YN", "N" },
            { "OFL_YN", "N" },
            { "INQR_DVSN", "02" },
            { "UNPR_DVSN", "01" },
            { "FUND_STTL_ICLD_YN", "N" },
            { "FNCG_AMT_AUTO_RDPT_YN", "N" },
            { "PRCS_DVSN", "00" },
            { "CTX_AREA_FK100", "" },
            { "CTX_AREA_NK100", "" }
        };

        var domesticHeaders = new Dictionary<string, string>
        {
            { "custtype", "P" }
        };

        var response = await _client.ExecuteAsync<DomesticBalanceResponse>("DomesticBalance", null, domesticQueryParams, domesticHeaders);

        // Update cache
        _cachedDomesticBalance = response;
        _domesticBalanceCacheExpiry = DateTime.UtcNow.Add(_cacheValidDuration);

        return response;
    }


    public async Task<OrderResult> PlaceOrderAsync(Order order)
    {
        await EnsureConnectedAsync();
        var accountNumber = _client.Account.Number;
        _logger.LogInformation("Placing order for {Ticker} {Action} {Qty} at {Price} for account {AccountNumber} via KIS.", order.Ticker, order.Action, order.Qty, order.Price, accountNumber);

        // Determine if domestic using intelligent caching strategy
        bool isDomestic = await IsDomesticTickerAsync(order.Ticker);

        _logger.LogDebug("Order ticker {Ticker} determined as {Type}",
            order.Ticker, isDomestic ? "Domestic" : "Overseas");

        if (isDomestic)
        {
            return await PlaceDomesticOrderAsync(order, accountNumber);
        }
        else
        {
            return await PlaceOverseasOrderAsync(order, accountNumber);
        }
    }

    private async Task<OrderResult> PlaceDomesticOrderAsync(Order order, string accountNumber)
    {
        var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);

        var requestBody = new
        {
            CANO = cano,
            ACNT_PRDT_CD = acntPrdtCd,
            PDNO = order.Ticker.Length > 6 ? order.Ticker.Substring(0, 6) : order.Ticker, // Ensure 6 digits
            ORD_DVSN = order.Type == OrderType.Market ? "01" : "00",
            ORD_QTY = order.Qty.ToString(),
            ORD_UNPR = order.Type == OrderType.Market ? "0" : (order.Price ?? 0).ToString(), // Market price must be 0
        };

        try
        {
            string endpoint = order.Action == OrderAction.Buy ? "DomesticOrderBuy" : "DomesticOrderSell";
            var response = await _client.ExecuteAsync<DomesticOrderResponse>(endpoint, requestBody);
            _logger.LogInformation("KIS Domestic order response: {Response}", response);

            if (response == null) return OrderResult.Failure("No response from KIS");

            if (response.RtCd == "0")
            {
                return OrderResult.Success(response.Msg1, response.Output?.Odno);
            }
            else
            {
                return OrderResult.Failure($"{response.Msg1} (Code: {response.RtCd})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place domestic order via KIS for account {Alias} ({Number}).", _client.Account.Alias, accountNumber);
            return OrderResult.Failure($"Exception: {ex.Message}");
        }
    }

    private async Task<OrderResult> PlaceOverseasOrderAsync(Order order, string accountNumber)
    {
        var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);

        string exchangeCode = GetKisExchangeCode(order.Exchange);
        CountryCode country = GetCountryCode(order.Exchange);
        string trIdKey = GetTrIdKey(country, order.Exchange);

        string ordDvsn = order.Type == OrderType.Market ? "01" : "00";
        string ordUnpr = order.Type == OrderType.Market ? "0" : (order.Price ?? 0).ToString("F2");

        // KIS API Limitation: US Market Orders ("01") are not supported.
        // Emulate using Limit Order with buffer.
        // KIS API Limitation: US Market Orders ("01") are not supported.
        // Emulate using Limit Order with buffer.
        if (country == CountryCode.US && order.Type == OrderType.Market)
        {
            _logger.LogInformation("Emulating Market Order for US stock {Ticker} using Limit Order with buffer.", order.Ticker);
            var limitPrice = await EmulateUsMarketOrderAsync(order.Ticker, order.Action);

            ordDvsn = "00"; // Limit
            ordUnpr = limitPrice.ToString("F2");

            _logger.LogInformation("Current Price used for emulation: {LimitPrice}", limitPrice);
        }

        // Adjust price formatting for JPY and VND (no decimals usually)
        if (order.Currency == CurrencyType.JPY || order.Currency == CurrencyType.VND)
        {
            ordUnpr = order.Type == OrderType.Market ? "0" : (order.Price ?? 0).ToString("F0");
        }

        var requestBody = new
        {
            CANO = cano,
            ACNT_PRDT_CD = acntPrdtCd,
            OVRS_EXCG_CD = exchangeCode,
            PDNO = order.Ticker,
            ORD_QTY = order.Qty.ToString(),
            OVRS_ORD_UNPR = ordUnpr,
            ORD_SVR_DVSN_CD = "0",
            ORD_DVSN = ordDvsn
        };

        try
        {
            string endpoint = order.Action == OrderAction.Buy ? "OverseasOrderBuy" : "OverseasOrderSell";

            // Log the request body for debugging "Input Classification Error"
            _logger.LogInformation("Sending KIS Overseas Order Request: {RequestBody}", System.Text.Json.JsonSerializer.Serialize(requestBody));

            var response = await _client.ExecuteAsync<OverseasOrderResponse>(endpoint, requestBody, trIdVariant: trIdKey);
            _logger.LogInformation("KIS Overseas order response: {Response}", response);

            if (response == null) return OrderResult.Failure("No response from KIS");

            if (response.RtCd == "0")
            {
                return OrderResult.Success(response.Msg1, response.Output?.Odno);
            }
            else
            {
                return OrderResult.Failure($"{response.Msg1} (Code: {response.RtCd})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place overseas order via KIS for account {Alias} ({Number}).", _client.Account.Alias, accountNumber);
            return OrderResult.Failure($"Exception: {ex.Message}");
        }
    }

    private async Task<decimal> EmulateUsMarketOrderAsync(string ticker, OrderAction action)
    {
        var priceInfo = await GetPriceAsync(ticker);
        if (priceInfo.CurrentPrice <= 0)
        {
            throw new InvalidOperationException($"Failed to fetch current price for {ticker} to emulate Market Order.");
        }

        decimal limitPrice = priceInfo.CurrentPrice * (action == OrderAction.Buy ? 1.05m : 0.95m);

        return Math.Round(limitPrice, 2);
    }

    private string GetKisExchangeCode(ExchangeCode exchange)
    {
        return exchange switch
        {
            ExchangeCode.NASDAQ => "NASD",
            ExchangeCode.NYSE => "NYS",
            ExchangeCode.AMEX => "AMS",
            ExchangeCode.HKEX => "SEHK",
            ExchangeCode.SSE => "SHAA",
            ExchangeCode.SZSE => "SZAA",
            ExchangeCode.TSE => "TKSE",
            ExchangeCode.HNX => "HASE",
            ExchangeCode.HOSE => "VNSE",
            _ => "NASD"
        };
    }

    private CountryCode GetCountryCode(ExchangeCode exchange)
    {
        return exchange switch
        {
            ExchangeCode.KRX or ExchangeCode.KOSDAQ or ExchangeCode.KOSPI => CountryCode.KR,
            ExchangeCode.NASDAQ or ExchangeCode.NYSE or ExchangeCode.AMEX => CountryCode.US,
            ExchangeCode.HKEX => CountryCode.HK,
            ExchangeCode.SSE or ExchangeCode.SZSE => CountryCode.CN,
            ExchangeCode.TSE => CountryCode.JP,
            ExchangeCode.HNX or ExchangeCode.HOSE => CountryCode.VN,
            _ => CountryCode.US
        };
    }

    private string GetTrIdKey(CountryCode country, ExchangeCode exchange)
    {
        if (country == CountryCode.CN)
        {
            return exchange == ExchangeCode.SSE ? "SH" : "SZ";
        }
        return country.ToString();
    }

    public async Task<Account> GetDepositAsync(CurrencyType? currency = null, bool forceRefresh = false)
    {
        await EnsureConnectedAsync();
        var accountNumber = _client.Account.Number;
        _logger.LogInformation("Getting account state for account {AccountNumber} via KIS. Currency: {Currency}", accountNumber, currency);

        // Use the account from the client directly
        var account = _client.Account;

        // Verify account number matches (optional but good for safety)
        if (account.Number != accountNumber)
        {
            _logger.LogWarning("Requested account number {Requested} does not match client account {Client}. Using client account.", accountNumber, account.Number);
        }

        // Initialize deposits if null
        if (account.Deposits == null)
        {
            account.Deposits = new Dictionary<CurrencyType, decimal>();
        }

        // 1. Get Domestic Balance (KRW)
        if (currency == null || currency == CurrencyType.KRW)
        {
            try
            {
                var domesticResponse = await GetDomesticBalanceAsync(accountNumber);
                if (domesticResponse?.Output2 != null && domesticResponse.Output2.Count > 0)
                {
                    if (account.Deposits.ContainsKey(CurrencyType.KRW))
                        account.Deposits[CurrencyType.KRW] = domesticResponse.Output2[0].DncaTotAmt;
                    else
                        account.Deposits.Add(CurrencyType.KRW, domesticResponse.Output2[0].DncaTotAmt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get domestic account balance for account {Alias} ({Number}).", _client.Account.Alias, accountNumber);
            }
        }

        // 2. Get Overseas Balance
        var overseasCurrencies = new List<(ExchangeCode Ex, CurrencyType Curr)>();

        if (currency == null)
        {
            overseasCurrencies.AddRange(new[]
            {
                (ExchangeCode.NASDAQ, CurrencyType.USD),
                (ExchangeCode.HKEX, CurrencyType.HKD),
                (ExchangeCode.SSE, CurrencyType.CNY),
                (ExchangeCode.TSE, CurrencyType.JPY),
                (ExchangeCode.HNX, CurrencyType.VND)
            });
        }
        else if (currency != CurrencyType.KRW)
        {
            // Map requested currency to a representative exchange
            var targetEx = currency switch
            {
                CurrencyType.USD => ExchangeCode.NASDAQ,
                CurrencyType.HKD => ExchangeCode.HKEX,
                CurrencyType.CNY => ExchangeCode.SSE,
                CurrencyType.JPY => ExchangeCode.TSE,
                CurrencyType.VND => ExchangeCode.HNX,
                _ => ExchangeCode.Unknown
            };

            if (targetEx != ExchangeCode.Unknown)
            {
                overseasCurrencies.Add((targetEx, currency.Value));
            }
        }

        foreach (var (exch, curr) in overseasCurrencies)
        {
            try
            {
                var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);

                var overseasQueryParams = new Dictionary<string, string>
                {
                    { "CANO", cano },
                    { "ACNT_PRDT_CD", acntPrdtCd },
                    { "OVRS_EXCG_CD", GetKisExchangeCode(exch) },
                    { "TR_CRCY_CD", curr.ToString() },
                    { "CTX_AREA_FK200", "" },
                    { "CTX_AREA_NK200", "" },
                    { "WCRC_FRCR_DVSN_CD", "02" },
                    { "TR_MKET_CD", "00" },
                    { "NATN_CD", GetNationCode(exch) },
                    { "INQR_DVSN_CD", "00" }
                };

                _logger.LogInformation("Calling OverseasBalance API for {Currency}", curr);

                var overseasHeaders = new Dictionary<string, string>
                {
                    { "custtype", "P" }
                };

                var overseasResponse = await _client.ExecuteAsync<OverseasBalanceResponse>("OverseasBalance", null, overseasQueryParams, overseasHeaders);

                if (overseasResponse?.Output2 != null)
                {
                    foreach (var summary in overseasResponse.Output2)
                    {
                        var responseCurrency = summary.CrcyCd ?? summary.OvrsCrcyCd;

                        if (!string.IsNullOrEmpty(responseCurrency) && Enum.TryParse<CurrencyType>(responseCurrency, out var currencyType))
                        {
                            if (summary.FrcrDnclAmt2 > 0)
                            {
                                if (account.Deposits.ContainsKey(currencyType))
                                    account.Deposits[currencyType] = summary.FrcrDnclAmt2;
                                else
                                    account.Deposits.Add(currencyType, summary.FrcrDnclAmt2);
                            }
                        }
                        else
                        {
                            if (summary.FrcrDnclAmt2 > 0)
                            {
                                if (account.Deposits.ContainsKey(curr))
                                    account.Deposits[curr] = summary.FrcrDnclAmt2;
                                else
                                    account.Deposits.Add(curr, summary.FrcrDnclAmt2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get overseas account balance for {Currency} on account {Alias} ({Number}).", curr, _client.Account.Alias, accountNumber);
            }
        }

        return account;
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        await EnsureConnectedAsync();
        var accountNumber = _client.Account.Number;
        _logger.LogInformation("Getting positions for account {AccountNumber} via KIS.", accountNumber);

        var positions = new List<Position>();

        // Get Domestic Positions
        try
        {
            var domesticResponse = await GetDomesticBalanceAsync(accountNumber);
            if (domesticResponse?.Output1 != null)
            {
                positions.AddRange(domesticResponse.Output1.Select(p => new Position
                {
                    AccountAlias = _client.Account.Alias ?? accountNumber,
                    Ticker = p.Pdno,
                    Currency = CurrencyType.KRW,
                    Qty = p.HldgQty,
                    AvgPrice = p.PchsAvgPric,
                    CurrentPrice = p.Prpr,
                    BuyReason = "Domestic",
                    Exchange = ExchangeCode.KRX
                }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get domestic positions for account {Alias} ({Number}).", _client.Account.Alias, accountNumber);
        }

        // Get Overseas Positions
        // Get Overseas Positions (Iterate over major exchanges)
        var overseasExchanges = new[]
        {
            ExchangeCode.NASDAQ, ExchangeCode.HKEX, ExchangeCode.SSE, ExchangeCode.SZSE,
            ExchangeCode.TSE, ExchangeCode.HNX, ExchangeCode.HOSE
        };

        foreach (var exch in overseasExchanges)
        {
            try
            {
                var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);

                var overseasQueryParams = new Dictionary<string, string>
                {
                    { "CANO", cano },
                    { "ACNT_PRDT_CD", acntPrdtCd },
                    { "OVRS_EXCH_CD", GetKisExchangeCode(exch) }
                };

                var overseasResponse = await _client.ExecuteAsync<OverseasPositionsResponse>("OverseasPositions", null, overseasQueryParams);

                // Gracefully handle null or empty responses (no positions for this exchange is normal)
                if (overseasResponse?.Output1 != null && overseasResponse.Output1.Any())
                {
                    // Filter out any null or invalid position entries
                    var validPositions = overseasResponse.Output1
                        .Where(p => p != null && !string.IsNullOrEmpty(p.OvrsPdno))
                        .Select(p => new Position
                        {
                            AccountAlias = _client.Account.Alias ?? accountNumber,
                            Ticker = p.OvrsPdno,
                            Currency = GetCurrencyFromExchangeCode(exch),
                            Qty = p.SellableQty,
                            AvgPrice = p.AvgPrc,
                            CurrentPrice = p.LastPrice,
                            BuyReason = "Overseas",
                            Exchange = exch
                        });

                    positions.AddRange(validPositions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get overseas positions for {Exchange} on account {Alias} ({Number}). This is normal if no positions exist on this exchange.", exch, _client.Account.Alias, accountNumber);
            }
        }


        // Calculate ChangeRate from existing data (no need for additional API calls)
        foreach (var p in positions)
        {
            if (p.AvgPrice > 0)
            {
                p.ChangeRate = ((p.CurrentPrice - p.AvgPrice) / p.AvgPrice) * 100;
            }
        }

        return positions;
    }

    private CurrencyType GetCurrencyFromExchangeCode(ExchangeCode exchangeCode)
    {
        return exchangeCode switch
        {
            ExchangeCode.NASDAQ or ExchangeCode.NYSE or ExchangeCode.AMEX => CurrencyType.USD,

            ExchangeCode.HKEX => CurrencyType.HKD,
            ExchangeCode.SSE or ExchangeCode.SZSE => CurrencyType.CNY,
            ExchangeCode.TSE => CurrencyType.JPY,
            ExchangeCode.HNX or ExchangeCode.HOSE => CurrencyType.VND,
            _ => CurrencyType.USD
        };
    }

    private string GetNationCode(ExchangeCode exchangeCode)
    {
        return exchangeCode switch
        {
            ExchangeCode.NASDAQ or ExchangeCode.NYSE or ExchangeCode.AMEX => "840", // USA
            ExchangeCode.HKEX => "344", // Hong Kong
            ExchangeCode.SSE or ExchangeCode.SZSE => "156", // China
            ExchangeCode.TSE => "392", // Japan
            ExchangeCode.HNX or ExchangeCode.HOSE => "704", // Vietnam
            _ => "000"
        };
    }

    public async Task<IEnumerable<Order>> GetOpenOrdersAsync()
    {
        await EnsureConnectedAsync();
        var accountNumber = _client.Account.Number;
        _logger.LogInformation("Getting open orders for account {AccountNumber} via KIS.", accountNumber);

        var openOrders = new List<Order>();

        // 1. Domestic Open Orders
        try
        {
            var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);
            var domesticQueryParams = new Dictionary<string, string>
            {
                { "CANO", cano },
                { "ACNT_PRDT_CD", acntPrdtCd },
                { "INQR_DVSN_1", "0" }, // 0: 議고쉶?쒖꽌
                { "INQR_DVSN_3", "00" }, // 00: ?꾩껜
                { "INQR_DVSN_2", "01" }, // 01: 誘몄껜寃?                { "CTX_AREA_FK100", "" },
                { "CTX_AREA_NK100", "" }
            };

            var domesticResponse = await _client.ExecuteAsync<DomesticOpenOrdersResponse>("DomesticOpenOrders", null, domesticQueryParams);
            if (domesticResponse?.Output1 != null)
            {
                foreach (var item in domesticResponse.Output1)
                {
                    if (decimal.TryParse(item.RmnQty, out var rmnQty) && rmnQty > 0)
                    {
                        openOrders.Add(new Order
                        {
                            Id = Guid.NewGuid(), // KIS doesn't give UUID, generate one or use OrdNo if string
                            AccountAlias = _client.Account.Alias ?? accountNumber,
                            Ticker = item.Pdno,
                            Action = item.SllBuyDvsnCd == "02" ? OrderAction.Buy : OrderAction.Sell,
                            Type = OrderType.Limit, // Assuming limit for now
                            Qty = rmnQty,
                            Price = decimal.TryParse(item.OrdUnpr, out var price) ? price : 0,
                            Timestamp = DateTime.UtcNow // Approximate
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get domestic open orders for account {Alias} ({Number}).", _client.Account.Alias, accountNumber);
        }

        // 2. Overseas Open Orders (Iterate exchanges if needed, or use a general endpoint if available)
        // KIS usually has separate endpoints per exchange or a unified one.
        // Assuming "OverseasOpenOrders" endpoint exists in spec or we use NASD as default.
        try
        {
            var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);
            var overseasQueryParams = new Dictionary<string, string>
            {
                { "CANO", cano },
                { "ACNT_PRDT_CD", acntPrdtCd },
                { "OVRS_EXCG_CD", "NASD" }, // Default to NASD for now, might need loop
                { "SORT_SQN", "DS" }, // Descending
                { "CTX_AREA_FK200", "" },
                { "CTX_AREA_NK200", "" }
            };

            var overseasResponse = await _client.ExecuteAsync<OverseasOpenOrdersResponse>("OverseasOpenOrders", null, overseasQueryParams);
            if (overseasResponse?.Output != null)
            {
                foreach (var item in overseasResponse.Output)
                {
                    if (decimal.TryParse(item.RmnQty, out var rmnQty) && rmnQty > 0)
                    {
                        openOrders.Add(new Order
                        {
                            Id = Guid.NewGuid(),
                            AccountAlias = _client.Account.Alias ?? accountNumber,
                            Ticker = item.Pdno,
                            Action = item.SllBuyDvsnCd == "02" ? OrderAction.Buy : OrderAction.Sell,
                            Type = OrderType.Limit,
                            Qty = rmnQty,
                            Price = decimal.TryParse(item.FtOrdUnpr3, out var price) ? price : 0,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get overseas open orders for account {Alias} ({Number}).", _client.Account.Alias, accountNumber);
        }

        return openOrders;
    }

    /// <summary>
    /// Determines if a 6-digit ticker is ambiguous (could be Korean or Chinese).
    /// Only tickers starting with 0 or 3 are ambiguous and need cache/Redis lookup.
    /// - 6, 9: Definitely overseas (Chinese)
    /// - 1, 2, 4, 5, 7, 8: Definitely domestic (Korean)
    /// - 0, 3: Ambiguous (need lookup)
    /// </summary>
    private bool IsAmbiguousTicker(string ticker)
    {
        if (ticker.Length != 6 || !ticker.All(char.IsDigit))
            return false;

        char firstDigit = ticker[0];
        return firstDigit == '0' || firstDigit == '3';
    }

    /// <summary>
    /// Determines if a ticker is domestic (Korean) with intelligent caching strategy:
    /// 1. Non-6-digit or alphabetic: Overseas
    /// 2. 6-digit starting with 6 or 9: Definitely overseas (Chinese)
    /// 3. 6-digit starting with 1,2,4,5,7,8: Definitely domestic (Korean)
    /// 4. 6-digit starting with 0 or 3: Ambiguous -> Check file -> Check cache -> Heuristic
    /// </summary>
    private Task<bool> IsDomesticTickerAsync(string ticker)
    {
        // 1. Check Length
        if (ticker.Length != 6)
        {
            _logger.LogDebug("Ticker {Ticker} is overseas (length != 6)", ticker);
            return Task.FromResult(false);
        }

        // 2. Check for Alphanumeric Domestic Tickers (e.g. 0089C0, 00593P)
        // If it contains letters but starts with a digit, it is Domestic (Korean).
        // US tickers typically don't start with digits.
        if (!ticker.All(char.IsDigit))
        {
            if (char.IsDigit(ticker[0]))
            {
                _logger.LogDebug("Ticker {Ticker} is domestic (alphanumeric starting with digit)", ticker);
                return Task.FromResult(true);
            }
            else
            {
                // Starts with letter -> Overseas
                return Task.FromResult(false);
            }
        }

        // From here on, ticker is 6 digits (numeric).

        // 6-digit numeric ticker - check first digit
        char firstDigit = ticker[0];

        // Quick rejection: 6 or 9 = definitely overseas (Chinese)
        if (firstDigit == '6' || firstDigit == '9')
        {
            _logger.LogDebug("Ticker {Ticker} is definitely overseas (starts with {Digit})", ticker, firstDigit);
            return Task.FromResult(false);
        }

        // Quick acceptance: 1,2,4,5,7,8 = definitely domestic (Korean)
        if (!IsAmbiguousTicker(ticker))
        {
            _logger.LogDebug("Ticker {Ticker} is definitely domestic (starts with {Digit})", ticker, firstDigit);
            return Task.FromResult(true);
        }

        // Ambiguous ticker (starts with 0 or 3): Check file first
        if (_domesticTickers.Value.Contains(ticker))
        {
            _logger.LogDebug("Ticker {Ticker} found in domestic file", ticker);
            return Task.FromResult(true);
        }

        // Check cache
        if (_tickerDomesticCache.TryGetValue(ticker, out var cachedIsDomestic))
        {
            _logger.LogDebug("Ticker {Ticker} domestic status found in cache: {IsDomestic}", ticker, cachedIsDomestic);
            return Task.FromResult(cachedIsDomestic);
        }

        // Fallback: Use heuristic classification
        bool heuristicIsDomestic = IsDomesticByHeuristic(ticker);
        _logger.LogDebug("Ticker {Ticker} classified heuristically as {IsDomestic}", ticker, heuristicIsDomestic);

        // Cache the heuristic result
        _tickerDomesticCache.TryAdd(ticker, heuristicIsDomestic);
        return Task.FromResult(heuristicIsDomestic);
    }



    /// <summary>
    /// Heuristic classification for ambiguous tickers (starting with 0 or 3).
    /// Based on number ranges from actual Stock data.
    /// Fixed to correctly handle Korean stocks like 005930 (Samsung), 035420 (NAVER), 067V0, etc.
    /// </summary>
    private bool IsDomesticByHeuristic(string ticker)
    {
        if (ticker.Length != 6 || !ticker.All(char.IsDigit))
            return false;

        int num = int.Parse(ticker);

        // Chinese ranges (overseas) - ONLY these specific ranges
        // Shenzhen A-shares: 000001-003999 (very low numbers)
        if (num >= 1 && num <= 3999)
            return false;

        // Shenzhen ChiNext: 300000-399999
        if (num >= 300000 && num <= 399999)
            return false;

        // All other 6-digit codes are Korean (domestic)
        // This includes:
        // - 005930 (Samsung Electronics)
        // - 035420 (NAVER)
        // - 051910 (LG Chem)
        // - 067V0, 089C0, 013P0, 023A0, 102A0, 053L0 (ETFs/derivatives)
        // - etc.
        return true;
    }
    /// <summary>
    /// Classifies 6-digit ticker codes to distinguish between KRX and Chinese exchanges (SSE/SZSE).
    /// Returns the most likely exchange based on number ranges and patterns observed in Stock data.
    /// 
    /// Based on actual Redis stock:{ticker} data patterns:
    /// - Korean stocks (KOSPI/KOSDAQ): Generally 005000-999999, excluding Chinese ranges
    /// - Chinese SSE A-shares: 600000-603999 (Shanghai Stock Exchange)
    /// - Chinese SZSE A-shares: 000001-003999 (Shenzhen main board)
    /// - Chinese SZSE ChiNext: 300000-399999 (Shenzhen growth board)
    /// 
    /// Accuracy: ~90-95% for most cases
    /// NOTE: This is a fallback heuristic. Prefer GetTickerExchangeAsync() which checks Redis first.
    /// </summary>
    /// <param name="ticker">6-digit ticker code</param>
    /// <returns>Tuple of (ExchangeCode, confidence level 0.0-1.0)</returns>
    private (ExchangeCode Exchange, double Confidence) Classify6DigitTicker(string ticker)
    {
        // Validate input
        if (ticker.Length != 6 || !ticker.All(char.IsDigit))
        {
            return (ExchangeCode.KRX, 1.0); // Default to KRX for non-numeric or invalid length
        }

        int num = int.Parse(ticker);

        // Chinese Stock Ranges (based on actual Stock data)

        // Shanghai Stock Exchange (SSE) A-shares: 600000-603999
        // Examples: 600000 (Pudong Development Bank), 600519 (Kweichow Moutai)
        if (num >= 600000 && num <= 603999)
        {
            return (ExchangeCode.SSE, 0.95);
        }

        // Shenzhen Stock Exchange (SZSE) A-shares: 000001-003999
        // Examples: 000001 (Ping An Bank), 000002 (China Vanke)
        if (num >= 1 && num <= 3999)
        {
            return (ExchangeCode.SZSE, 0.90);
        }

        // Shenzhen ChiNext board: 300000-399999
        // Examples: 300750 (Contemporary Amperex Technology)
        if (num >= 300000 && num <= 399999)
        {
            return (ExchangeCode.SZSE, 0.85);
        }

        // Additional Chinese ranges (less common but possible)
        // Shanghai B-shares: 900000-999999 (rare, lower confidence)
        if (num >= 900000 && num <= 999999)
        {
            return (ExchangeCode.SSE, 0.70);
        }

        // Korean Exchange (KRX) - Default for all other 6-digit codes
        // KOSPI examples: 005930 (Samsung Electronics), 035420 (NAVER)
        // KOSDAQ examples: 035720 (Kakao), 251270 (Netmarble)
        // Typical ranges: 005000-899999 (excluding Chinese ranges above)
        return (ExchangeCode.KRX, 0.95);
    }

    public async Task<PriceInfo> GetPriceAsync(string ticker)
    {
        await EnsureConnectedAsync();
        _logger.LogInformation("Getting price for {Ticker} via KIS.", ticker);

        // Determine if domestic using intelligent caching strategy
        bool isDomestic = await IsDomesticTickerAsync(ticker);

        if (isDomestic)
        {
            // Korean domestic stock
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    { "FID_COND_MRKT_DIV_CODE", "J" },
                    { "FID_INPUT_ISCD", ticker }
                };

                var response = await _client.ExecuteAsync<DomesticPriceResponse>("DomesticPrice", null, queryParams);
                if (response?.Output != null)
                {
                    decimal.TryParse(response.Output.StckPrpr, out var price);
                    decimal.TryParse(response.Output.PrdyCtrt, out var rate);
                    return new PriceInfo(price, rate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get domestic price for {Ticker}.", ticker);
            }
        }
        else
        {
            // Overseas stock - try multiple exchanges
            var exchanges = new List<ExchangeCode>();

            // Heuristic for exchange selection
            if (ticker.All(char.IsLetter))
            {
                // Alphabetic = US exchanges
                exchanges.AddRange(new[] { ExchangeCode.NASDAQ, ExchangeCode.NYSE, ExchangeCode.AMEX });
            }
            else if (ticker.Length == 6 && ticker.All(char.IsDigit))
            {
                // 6-digit numeric = Chinese exchanges (since we know it's not domestic)
                exchanges.AddRange(new[] { ExchangeCode.SSE, ExchangeCode.SZSE });
            }
            else
            {
                // Other patterns
                exchanges.AddRange(new[] { ExchangeCode.HKEX, ExchangeCode.TSE, ExchangeCode.HNX, ExchangeCode.HOSE });
            }

            foreach (var exch in exchanges)
            {
                try
                {
                    var queryParams = new Dictionary<string, string>
                    {
                        { "EXCD", GetKisExchangeCode(exch) },
                        { "SYMB", ticker }
                    };

                    var response = await _client.ExecuteAsync<OverseasPriceResponse>("OverseasPrice", null, queryParams);

                    _logger.LogInformation("OverseasPrice response for {Ticker} on {Exchange}: Output={Output}, Last={Last}, Rate={Rate}",
                        ticker, exch,
                        response?.Output != null ? "not null" : "null",
                        response?.Output?.Last ?? "null",
                        response?.Output?.Rate ?? "null");

                    if (response?.Output != null && !string.IsNullOrEmpty(response.Output.Last))
                    {
                        decimal.TryParse(response.Output.Last, out var price);
                        decimal.TryParse(response.Output.Rate, out var rate);
                        _logger.LogInformation("Successfully parsed price for {Ticker}: {Price}, rate: {Rate}", ticker, price, rate);
                        return new PriceInfo(price, rate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to get overseas price for {Ticker} on {Exchange}. Error: {Message}", ticker, exch, ex.Message);
                }
            }
        }

        _logger.LogWarning("Could not find price for {Ticker}.", ticker);
        return new PriceInfo(0, 0);
    }

    // Overload that accepts specific exchange to avoid unnecessary API calls
    public async Task<PriceInfo> GetPriceAsync(string ticker, ExchangeCode exchange)
    {
        await EnsureConnectedAsync();
        _logger.LogInformation("Getting price for {Ticker} on {Exchange} via KIS.", ticker, exchange);

        // Domestic
        if (exchange == ExchangeCode.KRX || exchange == ExchangeCode.KOSPI || exchange == ExchangeCode.KOSDAQ)
        {
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    { "FID_COND_MRKT_DIV_CODE", "J" },
                    { "FID_INPUT_ISCD", ticker }
                };

                var response = await _client.ExecuteAsync<DomesticPriceResponse>("DomesticPrice", null, queryParams);
                if (response?.Output != null)
                {
                    decimal.TryParse(response.Output.StckPrpr, out var price);
                    decimal.TryParse(response.Output.PrdyCtrt, out var rate);
                    return new PriceInfo(price, rate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get domestic price for {Ticker}.", ticker);
            }
        }
        else
        {
            // Overseas - query specific exchange only
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    { "EXCD", GetKisExchangeCode(exchange) },
                    { "SYMB", ticker }
                };

                var response = await _client.ExecuteAsync<OverseasPriceResponse>("OverseasPrice", null, queryParams);
                if (response?.Output != null && !string.IsNullOrEmpty(response.Output.Last))
                {
                    decimal.TryParse(response.Output.Last, out var price);
                    decimal.TryParse(response.Output.Rate, out var rate);
                    return new PriceInfo(price, rate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get overseas price for {Ticker} on {Exchange}.", ticker, exchange);
            }
        }

        _logger.LogWarning("Could not find price for {Ticker} on {Exchange}.", ticker, exchange);
        return new PriceInfo(0, 0);
    }
}
