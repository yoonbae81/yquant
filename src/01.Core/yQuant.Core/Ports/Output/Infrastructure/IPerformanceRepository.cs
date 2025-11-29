using System.Collections.Generic;
using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IPerformanceRepository
{
    Task SaveAsync(PerformanceLog log);
    Task<IEnumerable<PerformanceLog>> GetLogsAsync(string accountAlias);
}
