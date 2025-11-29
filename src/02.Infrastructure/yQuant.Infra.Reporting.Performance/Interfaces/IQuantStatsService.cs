using yQuant.Core.Models;
using yQuant.Infra.Reporting.Performance.Models;

namespace yQuant.Infra.Reporting.Performance.Interfaces;

public interface IQuantStatsService
{
    QuantStatsReport GenerateReport(IEnumerable<PerformanceLog> logs, string strategyName = "Strategy");
    string GenerateCsvReport(IEnumerable<PerformanceLog> logs);
}
