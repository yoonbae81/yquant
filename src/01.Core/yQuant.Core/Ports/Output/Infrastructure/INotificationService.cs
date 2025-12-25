using System.Threading.Tasks;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface INotificationService
{
    /// <summary>
    /// Sends a notification message to the configured channel.
    /// </summary>
    /// <param name="message">The message content to send.</param>
    Task SendNotificationAsync(string message);
}
