using yQuant.Core.Models;

namespace yQuant.App.Web.Models;

public class ScheduledLiquidation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AccountAlias { get; set; }
    public string? CountryFilter { get; set; }  // "KR", "US", null = All
    public string? BuyReasonFilter { get; set; } // "Manual", "Schedule", "strategy1", "Unknown", null = All
    public ScheduleTimeMode TimeMode { get; set; }
    public TimeSpan TimeConfig { get; set; }
    public HashSet<DayOfWeek> DaysOfWeek { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? NextExecutionTime { get; set; }
    public string? Notes { get; set; }
}

// DTO for import/export without Id and AccountAlias
public class ScheduledLiquidationImportDto
{
    public string? CountryFilter { get; set; }
    public string? BuyReasonFilter { get; set; }
    public HashSet<DayOfWeek> DaysOfWeek { get; set; } = new();
    public ScheduleTimeMode TimeMode { get; set; }
    public TimeSpan TimeConfig { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
