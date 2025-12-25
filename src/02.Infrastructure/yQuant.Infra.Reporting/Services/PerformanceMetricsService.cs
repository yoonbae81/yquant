using yQuant.Core.Models;

namespace yQuant.Infra.Reporting.Services;

/// <summary>
/// 성과 지표 계산 서비스
/// CAGR, Sharpe Ratio, Sortino Ratio, MDD, Volatility 등을 계산
/// </summary>
public class PerformanceMetricsService
{
    private const double TradingDaysPerYear = 252.0;
    private const double RiskFreeRate = 0.02; // 2% annual risk-free rate

    /// <summary>
    /// CAGR (Compound Annual Growth Rate) 계산
    /// </summary>
    public double CalculateCAGR(decimal initialEquity, decimal finalEquity, int days)
    {
        if (initialEquity <= 0 || days <= 0)
            return 0;

        var years = days / 365.0;
        return Math.Pow((double)(finalEquity / initialEquity), 1.0 / years) - 1.0;
    }

    /// <summary>
    /// 누적 수익률 계산
    /// </summary>
    public double CalculateCumulativeReturn(decimal initialEquity, decimal finalEquity)
    {
        if (initialEquity <= 0)
            return 0;

        return (double)((finalEquity - initialEquity) / initialEquity);
    }

    /// <summary>
    /// Sharpe Ratio 계산 (연환산)
    /// </summary>
    public double CalculateSharpeRatio(IEnumerable<DailySnapshot> snapshots)
    {
        var returns = snapshots
            .OrderBy(s => s.Date)
            .Select(s => s.DailyReturn)
            .ToList();

        if (returns.Count < 2)
            return 0;

        var avgReturn = returns.Average();
        var stdDev = CalculateStandardDeviation(returns);

        if (stdDev == 0)
            return 0;

        // Annualize
        var annualizedReturn = avgReturn * TradingDaysPerYear;
        var annualizedStdDev = stdDev * Math.Sqrt(TradingDaysPerYear);

        return (annualizedReturn - RiskFreeRate) / annualizedStdDev;
    }

    /// <summary>
    /// Sortino Ratio 계산 (연환산)
    /// Sharpe와 유사하지만 하방 변동성만 고려
    /// </summary>
    public double CalculateSortinoRatio(IEnumerable<DailySnapshot> snapshots)
    {
        var returns = snapshots
            .OrderBy(s => s.Date)
            .Select(s => s.DailyReturn)
            .ToList();

        if (returns.Count < 2)
            return 0;

        var avgReturn = returns.Average();
        var downsideDeviation = CalculateDownsideDeviation(returns, 0);

        if (downsideDeviation == 0)
            return 0;

        // Annualize
        var annualizedReturn = avgReturn * TradingDaysPerYear;
        var annualizedDownsideDev = downsideDeviation * Math.Sqrt(TradingDaysPerYear);

        return (annualizedReturn - RiskFreeRate) / annualizedDownsideDev;
    }

    /// <summary>
    /// Maximum Drawdown (MDD) 계산
    /// </summary>
    public double CalculateMDD(IEnumerable<DailySnapshot> snapshots)
    {
        var equityCurve = snapshots
            .OrderBy(s => s.Date)
            .Select(s => s.TotalEquity)
            .ToList();

        if (equityCurve.Count == 0)
            return 0;

        decimal peak = equityCurve[0];
        double maxDrawdown = 0;

        foreach (var equity in equityCurve)
        {
            if (equity > peak)
            {
                peak = equity;
            }

            var drawdown = peak > 0 ? (double)((peak - equity) / peak) : 0;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }

    /// <summary>
    /// Volatility (변동성) 계산 (연환산)
    /// </summary>
    public double CalculateVolatility(IEnumerable<DailySnapshot> snapshots)
    {
        var returns = snapshots
            .OrderBy(s => s.Date)
            .Select(s => s.DailyReturn)
            .ToList();

        if (returns.Count < 2)
            return 0;

        var stdDev = CalculateStandardDeviation(returns);
        return stdDev * Math.Sqrt(TradingDaysPerYear); // Annualize
    }

    /// <summary>
    /// Win Rate 계산 (수익 거래 비율)
    /// </summary>
    public double CalculateWinRate(IEnumerable<TradeRecord> trades)
    {
        var closedTrades = GetClosedTrades(trades).ToList();

        if (closedTrades.Count == 0)
            return 0;

        var winningTrades = closedTrades.Count(t => t.PnL > 0);
        return (double)winningTrades / closedTrades.Count;
    }

    /// <summary>
    /// Profit Factor 계산 (총 수익 / 총 손실)
    /// </summary>
    public double CalculateProfitFactor(IEnumerable<TradeRecord> trades)
    {
        var closedTrades = GetClosedTrades(trades).ToList();

        var totalProfit = closedTrades.Where(t => t.PnL > 0).Sum(t => t.PnL);
        var totalLoss = Math.Abs(closedTrades.Where(t => t.PnL < 0).Sum(t => t.PnL));

        if (totalLoss == 0)
            return totalProfit > 0 ? double.PositiveInfinity : 0;

        return (double)(totalProfit / totalLoss);
    }

    /// <summary>
    /// 표준편차 계산
    /// </summary>
    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count < 2)
            return 0;

        var avg = valuesList.Average();
        var sumOfSquares = valuesList.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / (valuesList.Count - 1));
    }

    /// <summary>
    /// 하방 편차 계산 (목표 수익률 이하의 변동성만 고려)
    /// </summary>
    private double CalculateDownsideDeviation(IEnumerable<double> returns, double targetReturn)
    {
        var downsideReturns = returns
            .Where(r => r < targetReturn)
            .Select(r => Math.Pow(r - targetReturn, 2))
            .ToList();

        if (downsideReturns.Count == 0)
            return 0;

        return Math.Sqrt(downsideReturns.Average());
    }

    /// <summary>
    /// 매수-매도 쌍으로 청산된 거래 추출
    /// </summary>
    private IEnumerable<ClosedTrade> GetClosedTrades(IEnumerable<TradeRecord> trades)
    {
        var tradesList = trades.OrderBy(t => t.ExecutedAt).ToList();
        var positions = new Dictionary<string, List<TradeRecord>>();
        var closedTrades = new List<ClosedTrade>();

        foreach (var trade in tradesList)
        {
            if (!positions.ContainsKey(trade.Ticker))
            {
                positions[trade.Ticker] = new List<TradeRecord>();
            }

            if (trade.Action == OrderAction.Buy)
            {
                positions[trade.Ticker].Add(trade);
            }
            else if (trade.Action == OrderAction.Sell)
            {
                // FIFO 방식으로 매칭
                var remainingQty = trade.Quantity;

                while (remainingQty > 0 && positions[trade.Ticker].Any())
                {
                    var buyTrade = positions[trade.Ticker].First();
                    var matchedQty = Math.Min(buyTrade.Quantity, remainingQty);

                    var pnl = (trade.ExecutedPrice - buyTrade.ExecutedPrice) * matchedQty
                        - trade.Commission - buyTrade.Commission;

                    closedTrades.Add(new ClosedTrade
                    {
                        Ticker = trade.Ticker,
                        EntryDate = buyTrade.ExecutedAt,
                        ExitDate = trade.ExecutedAt,
                        Quantity = matchedQty,
                        EntryPrice = buyTrade.ExecutedPrice,
                        ExitPrice = trade.ExecutedPrice,
                        PnL = pnl
                    });

                    buyTrade.Quantity -= matchedQty;
                    remainingQty -= matchedQty;

                    if (buyTrade.Quantity <= 0)
                    {
                        positions[trade.Ticker].RemoveAt(0);
                    }
                }
            }
        }

        return closedTrades;
    }

    private class ClosedTrade
    {
        public required string Ticker { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal PnL { get; set; }
    }
}
