using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS.Models;

namespace yQuant.Infra.Broker.KIS;

public class KISBrokerAdapter : IBrokerAdapter
{
    private readonly ILogger<KISBrokerAdapter> _logger;
    private readonly IKISClient _client;

    public KISBrokerAdapter(IKISClient client, ILogger<KISBrokerAdapter> logger)
    {
        _logger = logger;
        _client = client;
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

    public async Task<OrderResult> PlaceOrderAsync(Order order)
    {
        await EnsureConnectedAsync();
        var accountNumber = _client.Account.Number;
        _logger.LogInformation("Placing order for {Ticker} {Action} {Qty} at {Price} for account {AccountNumber} via KIS.", order.Ticker, order.Action, order.Qty, order.Price, accountNumber);

        // Simple logic to distinguish Domestic vs Overseas based on Ticker length or format
        // This is a heuristic and might need a better approach (e.g. Order.Exchange property)
        bool isDomestic = order.Ticker.Length == 6 && int.TryParse(order.Ticker, out _);

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
            _logger.LogError(ex, "Failed to place domestic order via KIS.");
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
            _logger.LogError(ex, "Failed to place overseas order via KIS.");
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

    public async Task<Account> GetAccountStateAsync()
    {
        await EnsureConnectedAsync();
        var accountNumber = _client.Account.Number;
        _logger.LogInformation("Getting account state for account {AccountNumber} via KIS.", accountNumber);

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

        // Get Domestic Balance (KRW)
        try
        {
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

            _logger.LogInformation("Calling DomesticBalance API:");
            _logger.LogInformation("  BaseUrl: {BaseUrl}", _client.GetType().GetProperty("BaseUrl")?.GetValue(_client) ?? "N/A");
            _logger.LogInformation("  Endpoint: /uapi/domestic-stock/v1/trading/inquire-balance");
            _logger.LogInformation("  Query Parameters:");
            foreach (var param in domesticQueryParams)
            {
                _logger.LogInformation("    {Key} = {Value}", param.Key, param.Value);
            }

            var domesticHeaders = new Dictionary<string, string>
            {
                { "custtype", "P" }  // P = 媛쒖씤, B = 踰뺤씤
            };

            var domesticResponse = await _client.ExecuteAsync<DomesticBalanceResponse>("DomesticBalance", null, domesticQueryParams, domesticHeaders);
            if (domesticResponse?.Output2 != null && domesticResponse.Output2.Count > 0)
            {
                account.Deposits.Add(CurrencyType.KRW, domesticResponse.Output2[0].DncaTotAmt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get domestic account balance.");
        }

        // Get Overseas Balance (USD, HKD, CNY, JPY, VND)
        var overseasCurrencies = new[] 
        { 
        // Get Overseas Balance (USD, HKD, CNY, JPY, VND)
        var overseasCurrencies = new[] 
        { 
            (Ex: ExchangeCode.NASDAQ, Curr: CurrencyType.USD), 
            (Ex: ExchangeCode.HKEX, Curr: CurrencyType.HKD), 
            (Ex: ExchangeCode.SSE, Curr: CurrencyType.CNY), 
            (Ex: ExchangeCode.TSE, Curr: CurrencyType.JPY), 
            (Ex: ExchangeCode.HNX, Curr: CurrencyType.VND) 
        };

        foreach (var (exch, curr) in overseasCurrencies)
        {
            try
            {
                var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);
                
                var overseasQueryParams = new Dictionary<string, string>
                {
                    { "CANO", cano },
                    { "ACNT_PRDT_CD", acntPrdtCd },
                    { "OVRS_EXCG_CD", exch },
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

                var overseasHeaders = new Dictionary<string, string>
                {
                    { "custtype", "P" }
                };

                var overseasResponse = await _client.ExecuteAsync<OverseasBalanceResponse>("OverseasBalance", null, overseasQueryParams, overseasHeaders);
                
                if (overseasResponse?.Output2 != null)
                {
                    foreach (var summary in overseasResponse.Output2)
                    {
                        // If we have a currency code in the response, use it to match
                        var responseCurrency = summary.CrcyCd ?? summary.OvrsCrcyCd;
                        
                        if (!string.IsNullOrEmpty(responseCurrency))
                        {
                            if (Enum.TryParse<CurrencyType>(responseCurrency, out var currencyType))
                            {
                                if (!account.Deposits.ContainsKey(currencyType))
                                {
                                    account.Deposits.Add(currencyType, summary.FrcrDnclAmt2);
                                }
                            }
                        }
                        else 
                        {
                            // Fallback to previous logic if no currency code found (but log warning)
                            if (summary.FrcrDnclAmt2 > 0)
                            {
                                if (Enum.TryParse<CurrencyType>(curr, out var currencyType))
                                {
                            if (summary.FrcrDnclAmt2 > 0)
                            {
                                if (!account.Deposits.ContainsKey(curr))
                                {
                                    account.Deposits.Add(curr, summary.FrcrDnclAmt2);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get overseas account balance for {Currency}.", curr);
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

            var domesticResponse = await _client.ExecuteAsync<DomesticBalanceResponse>("DomesticBalance", null, domesticQueryParams);
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
                    Source = "Domestic"
                }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get domestic positions.");
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
                if (overseasResponse?.Output1 != null)
                {
                    positions.AddRange(overseasResponse.Output1.Select(p => new Position
                    {
                        AccountAlias = _client.Account.Alias ?? accountNumber,
                        Ticker = p.OvrsPdno,
                        Currency = GetCurrencyFromExchangeCode(exch),
                        Qty = p.SellableQty,
                        AvgPrice = p.AvgPrc,
                        CurrentPrice = p.LastPrice,
                        Source = "Overseas"
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get overseas positions for {Exchange}.", exch);
            }
        }



        // Enrich with Price Info (Change Rate)
        var tasks = positions.Select(async p => 
        {
            try
            {
                var priceInfo = await GetPriceAsync(p.Ticker);
                if (priceInfo.CurrentPrice > 0)
                {
                    p.CurrentPrice = priceInfo.CurrentPrice;
                    p.ChangeRate = priceInfo.ChangeRate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to update price for {Ticker}: {Message}", p.Ticker, ex.Message);
            }
        });
        
        await Task.WhenAll(tasks);

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
            _logger.LogError(ex, "Failed to get domestic open orders.");
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
            _logger.LogError(ex, "Failed to get overseas open orders.");
        }

        return openOrders;
    }
    public async Task<PriceInfo> GetPriceAsync(string ticker)
    {
        await EnsureConnectedAsync();
        _logger.LogInformation("Getting price for {Ticker} via KIS.", ticker);

        // 1. Determine if Domestic (6 digits)
        bool isDomestic = ticker.Length == 6 && int.TryParse(ticker, out _);

        if (isDomestic)
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
            // 2. Overseas
            // Try major exchanges: NASD, NYS, AMS, SEHK, SHAA, SZAA, TKSE, HASE, VNSE
            // Heuristic: Alphabetic -> US (NASD, NYS, AMS)
            // Numeric -> HK (SEHK), JP (TKSE), CN (SHAA, SZAA)
            
            var exchanges = new List<ExchangeCode>();
            if (ticker.All(char.IsLetter))
            {
                exchanges.AddRange(new[] { ExchangeCode.NASDAQ, ExchangeCode.NYSE, ExchangeCode.AMEX });
            }
            else
            {
                exchanges.AddRange(new[] { ExchangeCode.HKEX, ExchangeCode.TSE, ExchangeCode.SSE, ExchangeCode.SZSE, ExchangeCode.HNX, ExchangeCode.HOSE });
            }

            foreach (var exch in exchanges)
            {
                try
                {
                    var queryParams = new Dictionary<string, string>
                    {
                        { "AUTH", "" },
                        { "EXCD", GetKisExchangeCode(exch) },
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
                    _logger.LogWarning("Failed to get overseas price for {Ticker} on {Exchange}. Error: {Message}", ticker, exch, ex.Message);
                }
            }
        }

        _logger.LogWarning("Could not find price for {Ticker}.", ticker);
        return new PriceInfo(0, 0);
    }
}
