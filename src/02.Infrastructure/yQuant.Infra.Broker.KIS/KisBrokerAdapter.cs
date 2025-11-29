using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Trading.KIS.Models;

namespace yQuant.Infra.Broker.KIS;

public class KisBrokerAdapter : IBrokerAdapter
{
    private readonly ILogger<KisBrokerAdapter> _logger;
    private readonly IKisConnector _client;
    private readonly string _accountNoPrefix;
    private readonly string? _alias;

    public KisBrokerAdapter(ILogger<KisBrokerAdapter> logger, IKisConnector client, string accountNoPrefix, string? alias = null)
    {
        _logger = logger;
        _client = client;
        _accountNoPrefix = accountNoPrefix;
        _alias = alias;
    }

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

    public async Task<OrderResult> PlaceOrderAsync(Order order, string accountNumber)
    {
        await EnsureConnectedAsync();
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
            string endpoint = order.Action == OrderAction.Buy ? "DomesticBuyOrder" : "DomesticSellOrder";
            var response = await _client.ExecuteAsync<KisDomesticOrderResponse>(endpoint, requestBody);
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
        
        string kisExchangeCode = GetKisExchangeCode(order.Exchange);
        string countryCode = GetCountryCode(order.Exchange);
        
        var requestBody = new
        {
            CANO = cano,
            ACNT_PRDT_CD = acntPrdtCd,
            OVRS_EXCG_CD = kisExchangeCode,
            PDNO = order.Ticker,
            ORD_QTY = order.Qty.ToString(),
            OVRS_ORD_UNPR = order.Type == OrderType.Market ? "0" : (order.Price ?? 0).ToString("F2"), // Ensure 2 decimal places (might need adjustment for JPY/VND)
            ORD_SVR_DVSN_CD = "0",
            ORD_DVSN = order.Type == OrderType.Market ? "01" : "00"
        };

        // Adjust price formatting for JPY and VND (no decimals usually)
        if (order.Currency == CurrencyType.JPY || order.Currency == CurrencyType.VND)
        {
             // Use anonymous type with different property if needed, or just format string
             // But we are using object for requestBody. 
             // Let's recreate requestBody with correct formatting if needed.
             // Actually, "F2" might be rejected for JPY.
             var priceString = order.Type == OrderType.Market ? "0" : (order.Price ?? 0).ToString("F0");
             
             // Re-create anonymous object to override price
             requestBody = new
             {
                CANO = cano,
                ACNT_PRDT_CD = acntPrdtCd,
                OVRS_EXCG_CD = kisExchangeCode,
                PDNO = order.Ticker,
                ORD_QTY = order.Qty.ToString(),
                OVRS_ORD_UNPR = priceString,
                ORD_SVR_DVSN_CD = "0",
                ORD_DVSN = order.Type == OrderType.Market ? "01" : "00"
             };
        }

        try
        {
            string endpoint = order.Action == OrderAction.Buy ? "OverseasBuyOrder" : "OverseasSellOrder";
            var response = await _client.ExecuteAsync<KisOverseasOrderResponse>(endpoint, requestBody, trIdVariant: countryCode);
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

    private string GetKisExchangeCode(string exchange)
    {
        if (string.IsNullOrEmpty(exchange)) return "NASD"; // Default

        return exchange.ToUpper() switch
        {
            "NASDAQ" => "NASD",
            "NYSE" => "NYS",
            "AMEX" => "AMS",
            "HKEX" => "SEHK",
            "SSE" => "SHAA",
            "SZSE" => "SZAA",
            "TSE" => "TKSE",
            "HNX" => "HASE",
            "HOSE" => "VNSE",
            _ => "NASD"
        };
    }

    private string GetCountryCode(string exchange)
    {
        if (string.IsNullOrEmpty(exchange)) return "US"; // Default US

        return exchange.ToUpper() switch
        {
            "NASDAQ" => "US",
            "NYSE" => "US",
            "AMEX" => "US",
            "HKEX" => "HK",
            "SSE" => "SH",
            "SZSE" => "SZ",
            "TSE" => "JP",
            "HNX" => "VN",
            "HOSE" => "VN",
            _ => "US"
        };
    }

    public async Task<Account> GetAccountStateAsync(string accountNumber)
    {
        await EnsureConnectedAsync();
        _logger.LogInformation("Getting account state for account {AccountNumber} via KIS.", accountNumber);

        var account = new Account
        {
            Alias = _alias, // Account alias (e.g., "Main_Aggressive")
            Number = accountNumber,
            Broker = "KIS",
            Active = true,
            Deposits = new Dictionary<CurrencyType, decimal>()
        };

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
                { "custtype", "P" }  // P = 개인, B = 법인
            };

            var domesticResponse = await _client.ExecuteAsync<KisDomesticBalanceResponse>("DomesticBalance", null, domesticQueryParams, domesticHeaders);
            if (domesticResponse?.Output2 != null && domesticResponse.Output2.Count > 0)
            {
                // Use OrdPsblCash (Order Possible Cash) instead of DncaTotAmt (Total Deposit)
                account.Deposits.Add(CurrencyType.KRW, domesticResponse.Output2[0].OrdPsblCash);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get domestic account balance.");
        }

        // Get Overseas Balance (USD)
        // Get Overseas Balance (USD, HKD, CNY, JPY, VND)
        var overseasCurrencies = new[] 
        { 
            (Ex: "NAS", Curr: "USD"), 
            (Ex: "SEHK", Curr: "HKD"), 
            (Ex: "SHAA", Curr: "CNY"), 
            (Ex: "TKSE", Curr: "JPY"), 
            (Ex: "HASE", Curr: "VND") 
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
                    { "OVRS_EXCH_CD", exch },
                    { "TR_CRCY_CD", curr },
                    { "CTX_AREA_FK200", "" },
                    { "CTX_AREA_NK200", "" }
                };

                var overseasHeaders = new Dictionary<string, string>
                {
                    { "custtype", "P" }
                };

                var overseasResponse = await _client.ExecuteAsync<KisOverseasBalanceResponse>("OverseasBalance", null, overseasQueryParams, overseasHeaders);
                if (overseasResponse?.Output2 != null)
                {
                    if (overseasResponse.Output2.ForeignCash > 0)
                    {
                        if (Enum.TryParse<CurrencyType>(curr, out var currencyType))
                        {
                            if (!account.Deposits.ContainsKey(currencyType))
                            {
                                account.Deposits.Add(currencyType, overseasResponse.Output2.ForeignCash);
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

    public async Task<List<Position>> GetPositionsAsync(string accountNumber)
    {
        await EnsureConnectedAsync();
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

            var domesticResponse = await _client.ExecuteAsync<KisDomesticBalanceResponse>("DomesticBalance", null, domesticQueryParams);
            if (domesticResponse?.Output1 != null)
            {
                positions.AddRange(domesticResponse.Output1.Select(p => new Position
                {
                    AccountAlias = _alias ?? accountNumber,
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
        var overseasExchanges = new[] { "NAS", "SEHK", "SHAA", "SZAA", "TKSE", "HASE", "VNSE" };
        
        foreach (var exch in overseasExchanges)
        {
            try
            {
                var (cano, acntPrdtCd) = ParseAccountNumber(accountNumber);
                
                var overseasQueryParams = new Dictionary<string, string>
                {
                    { "CANO", cano },
                    { "ACNT_PRDT_CD", acntPrdtCd },
                    { "OVRS_EXCH_CD", exch }
                };

                var overseasResponse = await _client.ExecuteAsync<KisOverseasPositionsResponse>("OverseasPositions", null, overseasQueryParams);
                if (overseasResponse?.Output1 != null)
                {
                    positions.AddRange(overseasResponse.Output1.Select(p => new Position
                    {
                        AccountAlias = _alias ?? accountNumber,
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

    private CurrencyType GetCurrencyFromExchangeCode(string exchangeCode)
    {
        return exchangeCode switch
        {
            "NAS" or "NASD" or "NYS" or "AMS" => CurrencyType.USD,
            "SEHK" => CurrencyType.HKD,
            "SHAA" or "SZAA" => CurrencyType.CNY,
            "TKSE" => CurrencyType.JPY,
            "HASE" or "VNSE" => CurrencyType.VND,
            _ => CurrencyType.USD
        };
    }

    public async Task<IEnumerable<Order>> GetOpenOrdersAsync(string accountNumber)
    {
        await EnsureConnectedAsync();
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
                { "INQR_DVSN_1", "0" }, // 0: 조회순서
                { "INQR_DVSN_3", "00" }, // 00: 전체
                { "INQR_DVSN_2", "01" }, // 01: 미체결
                { "CTX_AREA_FK100", "" },
                { "CTX_AREA_NK100", "" }
            };

            var domesticResponse = await _client.ExecuteAsync<KisDomesticOpenOrdersResponse>("DomesticOpenOrders", null, domesticQueryParams);
            if (domesticResponse?.Output1 != null)
            {
                foreach (var item in domesticResponse.Output1)
                {
                    if (decimal.TryParse(item.RmnQty, out var rmnQty) && rmnQty > 0)
                    {
                        openOrders.Add(new Order
                        {
                            Id = Guid.NewGuid(), // KIS doesn't give UUID, generate one or use OrdNo if string
                            AccountAlias = _alias ?? accountNumber,
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

            var overseasResponse = await _client.ExecuteAsync<KisOverseasOpenOrdersResponse>("OverseasOpenOrders", null, overseasQueryParams);
            if (overseasResponse?.Output != null)
            {
                foreach (var item in overseasResponse.Output)
                {
                    if (decimal.TryParse(item.RmnQty, out var rmnQty) && rmnQty > 0)
                    {
                        openOrders.Add(new Order
                        {
                            Id = Guid.NewGuid(),
                            AccountAlias = _alias ?? accountNumber,
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

                var response = await _client.ExecuteAsync<KisDomesticPriceResponse>("DomesticPrice", null, queryParams);
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
            
            var exchanges = new List<string>();
            if (ticker.All(char.IsLetter))
            {
                exchanges.AddRange(new[] { "NAS", "NYS", "AMS" });
            }
            else
            {
                exchanges.AddRange(new[] { "SEHK", "TKSE", "SHAA", "SZAA", "HASE", "VNSE" });
            }

            foreach (var exch in exchanges)
            {
                try
                {
                    var queryParams = new Dictionary<string, string>
                    {
                        { "AUTH", "" },
                        { "EXCD", exch },
                        { "SYMB", ticker }
                    };

                    var response = await _client.ExecuteAsync<KisOverseasPriceResponse>("OverseasPrice", null, queryParams);
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

