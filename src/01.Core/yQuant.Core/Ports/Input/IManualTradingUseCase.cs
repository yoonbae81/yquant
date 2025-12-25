using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Input;

public interface IManualTradingUseCase
{
    /// <summary>
    /// Place a manual order directly.
    /// </summary>
    /// <param name="order">The order to place.</param>
    Task PlaceManualOrderAsync(Order order);
}
