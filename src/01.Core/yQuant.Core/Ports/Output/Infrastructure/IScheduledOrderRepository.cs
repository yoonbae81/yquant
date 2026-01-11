using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IScheduledOrderRepository
{
    Task<IEnumerable<ScheduledOrder>> GetAllAsync(string accountAlias);
    Task ProcessOrdersAsync(string accountAlias, Func<List<ScheduledOrder>, Task<bool>> processor, bool waitForLock = false);
    Task AddOrUpdateAsync(ScheduledOrder order);
    Task RemoveAsync(string accountAlias, Guid orderId);
}
