using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using yQuant.App.Console.Commands;

namespace yQuant.App.Console
{
    public class CommandRouter
    {
        private readonly IEnumerable<ICommand> _commands;

        public CommandRouter(IEnumerable<ICommand> commands)
        {
            _commands = commands;
        }

        public async Task ExecuteAsync(string commandName, string[] args)
        {
            var command = _commands.FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            if (command == null)
            {
                // Fuzzy matching: check if any command starts with the input
                var matches = _commands.Where(c => c.Name.StartsWith(commandName, StringComparison.OrdinalIgnoreCase)).ToList();
                
                if (matches.Count == 1)
                {
                    command = matches.First();
                }
                else if (matches.Count > 1)
                {
                    System.Console.WriteLine($"Ambiguous command '{commandName}'. Matches: {string.Join(", ", matches.Select(c => c.Name))}");
                    return;
                }
            }

            if (command != null)
            {
                await command.ExecuteAsync(args);
            }
            else
            {
                System.Console.WriteLine($"Unknown command: {commandName}");
                System.Console.WriteLine("Available commands:");
                foreach (var cmd in _commands)
                {
                    System.Console.WriteLine($"  {cmd.Name}: {cmd.Description}");
                }
            }
        }
    }
}
