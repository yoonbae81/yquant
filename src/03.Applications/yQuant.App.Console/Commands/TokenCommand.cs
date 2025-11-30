using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands
{
    public class TokenCommand : ICommand
    {
        private readonly IKISConnector _kisConnector;
        private readonly ILogger<TokenCommand> _logger;

        public TokenCommand(IKISConnector KISConnector, ILogger<TokenCommand> logger)
        {
            _kisConnector = KISConnector;
            _logger = logger;
        }

        public string Name => "token";
        public string Description => "Manage KIS API token (use -r to refresh)";

        public async Task ExecuteAsync(string[] args)
        {
            bool refreshToken = args.Contains("-r") || args.Contains("--refresh");

            if (refreshToken)
            {
                System.Console.WriteLine("Refreshing token...");
                await _kisConnector.InvalidateTokenAsync();
            }

            System.Console.WriteLine("Ensuring connection (getting token)...");
            try
            {
                await _kisConnector.EnsureConnectedAsync();
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
