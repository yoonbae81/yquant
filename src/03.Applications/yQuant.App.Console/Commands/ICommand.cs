using System.Threading.Tasks;

namespace yQuant.App.Console.Commands
{
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }
        Task ExecuteAsync(string[] args);
    }
}
