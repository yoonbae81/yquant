#!/usr/bin/env dotnet fsi

// 6-digit ticker classification demo
// Run with: dotnet fsi classify_ticker_demo.fsx

type ExchangeCode =
    | KRX
    | SSE
    | SZSE

let classify6DigitTicker (ticker: string) : ExchangeCode * float =
    if ticker.Length <> 6 || not (ticker |> Seq.forall System.Char.IsDigit) then
        (KRX, 1.0)
    else
        let num = int ticker
        
        // Shanghai Stock Exchange (SSE) A-shares: 600000-603999
        if num >= 600000 && num <= 603999 then
            (SSE, 0.95)
        // Shenzhen Stock Exchange (SZSE) A-shares: 000001-003999
        elif num >= 1 && num <= 3999 then
            (SZSE, 0.90)
        // Shenzhen ChiNext board: 300000-399999
        elif num >= 300000 && num <= 399999 then
            (SZSE, 0.85)
        // Shanghai B-shares: 900000-999999 (rare)
        elif num >= 900000 && num <= 999999 then
            (SSE, 0.70)
        // Default to Korean Exchange (KRX)
        else
            (KRX, 0.95)

// Test cases
let testCases = [
    // Korean stocks (KOSPI)
    ("005930", "Samsung Electronics (Korea KOSPI)")
    ("035420", "NAVER (Korea KOSPI)")
    ("051910", "LG Chem (Korea KOSPI)")
    
    // Korean stocks (KOSDAQ)
    ("035720", "Kakao (Korea KOSDAQ)")
    ("251270", "Netmarble (Korea KOSDAQ)")
    
    // Chinese SSE A-shares
    ("600000", "Pudong Development Bank (China SSE)")
    ("600519", "Kweichow Moutai (China SSE)")
    ("601398", "Industrial & Commercial Bank (China SSE)")
    
    // Chinese SZSE A-shares
    ("000001", "Ping An Bank (China SZSE)")
    ("000002", "China Vanke (China SZSE)")
    ("000858", "Wuliangye Yibin (China SZSE)")
    
    // Chinese SZSE ChiNext
    ("300750", "Contemporary Amperex (China ChiNext)")
    ("300059", "East Money Information (China ChiNext)")
    
    // Edge cases
    ("000000", "Edge case: 000000 (likely invalid)")
    ("900001", "Edge case: SSE B-share (rare)")
    ("100000", "Mid-range Korean stock")
]

printfn "╔════════════════════════════════════════════════════════════════════════╗"
printfn "║         6-Digit Ticker Classification Test Results                    ║"
printfn "║         Based on StockMaster Redis Data Patterns                       ║"
printfn "╚════════════════════════════════════════════════════════════════════════╝"
printfn ""

for (ticker, description) in testCases do
    let (exchange, confidence) = classify6DigitTicker ticker
    let confidenceStr = sprintf "%.0f%%" (confidence * 100.0)
    printfn "%-6s: %-4s (%5s) - %s" ticker (string exchange) confidenceStr description

printfn ""
printfn "Classification Rules (based on actual StockMaster data):"
printfn "  • 600000-603999 → SSE  (95%% confidence) - Shanghai A-shares"
printfn "  • 000001-003999 → SZSE (90%% confidence) - Shenzhen A-shares"
printfn "  • 300000-399999 → SZSE (85%% confidence) - Shenzhen ChiNext"
printfn "  • 900000-999999 → SSE  (70%% confidence) - Shanghai B-shares (rare)"
printfn "  • Others        → KRX  (95%% confidence) - Korean stocks"
printfn ""
printfn "Note: This classification is used when Redis stock:{ticker} data is unavailable."
printfn "      For maximum accuracy, StockMaster should be run to populate Redis cache."
