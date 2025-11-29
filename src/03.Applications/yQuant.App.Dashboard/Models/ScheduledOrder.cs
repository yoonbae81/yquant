using yQuant.Core.Models; // For OrderAction, OrderType, CurrencyType

namespace yQuant.App.Dashboard.Models;

public class ScheduledOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AccountAlias { get; set; }
    public required string Ticker { get; set; }
    public required OrderAction Action { get; set; }
    public decimal TargetAmount { get; set; } // Target investment amount for buy, or value for sell
    public int? Quantity { get; set; } // Specific quantity to buy/sell, if not calculated from TargetAmount
    public DateTime ScheduledTime { get; set; } // UTC time for execution
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; } // Daily, Weekly, etc.
    public DateTime? LastExecutedTime { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
