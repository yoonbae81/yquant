using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands
{
    public class AuthCommand : ICommand
    {
        private readonly IKISClient _kisClient;
        private readonly ILogger<AuthCommand> _logger;

        public AuthCommand(IKISClient kisClient, ILogger<AuthCommand> logger)
        {
            _kisClient = kisClient;
            _logger = logger;
        }

        public string Name => "auth";
        public string Description => "Manage KIS API token (use -r to refresh)";

        public async Task ExecuteAsync(string[] args)
        {
            bool refreshToken = args.Contains("-r") || args.Contains("--refresh");

            if (refreshToken)
            {
                System.Console.WriteLine("Refreshing token...");
                await _kisClient.InvalidateTokenAsync();
            }

            System.Console.WriteLine("Ensuring connection (getting token)...");
            try
            {
                await _kisClient.EnsureConnectedAsync();
                System.Console.WriteLine("Successfully connected and token received.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get token.");
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
