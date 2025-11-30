using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands
{
    public class AuthCommand : ICommand
    {
        private readonly KISAdapterFactory _factory;
        private readonly ILogger<AuthCommand> _logger;

        private readonly string _targetAlias;

        public AuthCommand(KISAdapterFactory factory, ILogger<AuthCommand> logger, string targetAlias)
        {
            _factory = factory;
            _logger = logger;
            _targetAlias = targetAlias;
        }

        public string Name => "auth";
        public string Description => "Manage KIS API token (use -r to refresh)";

        public async Task ExecuteAsync(string[] args)
        {
            var alias = _targetAlias;
            if (alias == null)
            {
                System.Console.WriteLine("No KIS accounts found.");
                return;
            }

            System.Console.WriteLine($"Target Account: {alias}");

            bool refreshToken = args.Contains("-r") || args.Contains("--refresh");

            if (refreshToken)
            {
                System.Console.WriteLine("Refreshing token...");
                await _factory.InvalidateTokenAsync(alias);
            }

            System.Console.WriteLine("Ensuring connection (getting token)...");
            try
            {
                await _factory.EnsureConnectedAsync(alias);
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
