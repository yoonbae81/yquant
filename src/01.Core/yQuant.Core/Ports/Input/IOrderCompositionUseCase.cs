using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Input;

public interface IOrderCompositionUseCase
{
    /// <summary>
    /// Process an incoming trading signal, apply market rules and position sizing, and place an order if valid.
    /// </summary>
    /// <param name="signal">The trading signal to process.</param>
    Task ProcessSignalAsync(Signal signal);
}
