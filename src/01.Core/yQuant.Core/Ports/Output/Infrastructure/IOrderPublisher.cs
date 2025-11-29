using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IOrderPublisher
{
    /// <summary>
    /// Publishes an order to the system (e.g., via message bus).
    /// </summary>
    /// <param name="order">The order to publish.</param>
    Task PublishOrderAsync(Order order);
}
