using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands;

public class TestCommand : ICommand
{
    private readonly IKISClient _client;
    private readonly KISBrokerAdapter _adapter;
    private readonly ILogger<TestCommand> _logger;

    public string Name => "test";
    public string Description => "Test KIS API connection and retrieve account state";

    public TestCommand(IKISClient client, KISBrokerAdapter adapter, ILogger<TestCommand> logger)
    {
        _client = client;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task ExecuteAsync(string[] args)
    {
        bool refreshToken = args.Contains("-r") || args.Contains("--refresh-token");

        System.Console.WriteLine("🚀 KIS Account Connection Test");
        System.Console.WriteLine("================================\n");

        if (refreshToken)
        {
            System.Console.WriteLine("♻️  Token refresh mode enabled - will invalidate existing token\n");
            await _client.InvalidateTokenAsync();
            System.Console.WriteLine("✅ Token invalidated\n");
        }

        System.Console.WriteLine("Test 1: Connecting to KIS API (getting access token)...");
        await _client.EnsureConnectedAsync();
        System.Console.WriteLine("✅ Successfully connected to KIS API!\n");

        System.Console.WriteLine("Test 2: Getting account balance...");
        var accountState = await _adapter.GetAccountStateAsync();
        
        System.Console.WriteLine($"✅ Account retrieved:");
        System.Console.WriteLine($"   Account ID: {accountState.Alias}");
        System.Console.WriteLine($"   Broker: {accountState.Broker}");
        System.Console.WriteLine($"   Active: {accountState.Active}");
        System.Console.WriteLine($"   Deposits: {accountState.Deposits.Count} currencies\n");

        foreach (var deposit in accountState.Deposits)
        {
            System.Console.WriteLine($"   {deposit.Key}: {deposit.Value:N0}");
        }

        System.Console.WriteLine("\n================================");
        System.Console.WriteLine("✅ All tests passed!");
        System.Console.WriteLine($"KIS Account '{accountState.Alias}' is ready!");
    }
}
