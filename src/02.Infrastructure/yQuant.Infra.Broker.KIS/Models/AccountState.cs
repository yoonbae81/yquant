namespace yQuant.Infra.Trading.KIS.Models;

public class AccountState
{
    public string AccountId { get; set; } = string.Empty;
    public decimal TotalEquity { get; set; }
    public decimal? DepositKRW { get; set; } // Representing Deposit
    public decimal? DepositUSD { get; set; } // Assuming KISAdapter might return multiple currencies
    public decimal DailyPnL { get; set; }
    public double DailyPnLPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
