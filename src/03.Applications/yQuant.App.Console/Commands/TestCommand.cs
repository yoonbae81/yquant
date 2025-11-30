using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS;

namespace yQuant.App.Console.Commands;

public class TestCommand : ICommand
{
    private readonly KISAdapterFactory _factory;
    private readonly ILogger<TestCommand> _logger;

    private readonly string _targetAlias;

    public string Name => "test";
    public string Description => "Test KIS API connection and retrieve account state";

    public TestCommand(KISAdapterFactory factory, ILogger<TestCommand> logger, string targetAlias)
    {
        _factory = factory;
        _logger = logger;
        _targetAlias = targetAlias;
    }

    public async Task ExecuteAsync(string[] args)
    {
        var alias = _targetAlias;
        if (alias == null)
        {
            System.Console.WriteLine("No KIS accounts found.");
            return;
        }
        System.Console.WriteLine($"Target Account: {alias}");

        bool refreshToken = args.Contains("-r") || args.Contains("--refresh-token");

        System.Console.WriteLine("🚀 KIS Account Connection Test");
        System.Console.WriteLine("================================\n");

        if (refreshToken)
        {
            System.Console.WriteLine("♻️  Token refresh mode enabled - will invalidate existing token\n");
            await _factory.InvalidateTokenAsync(alias);
            System.Console.WriteLine("✅ Token invalidated\n");
        }

        System.Console.WriteLine("Test 1: Connecting to KIS API (getting access token)...");
        await _factory.EnsureConnectedAsync(alias);
        System.Console.WriteLine("✅ Successfully connected to KIS API!\n");

        System.Console.WriteLine("Test 2: Getting account balance...");
        var adapter = _factory.GetAdapter(alias);
        if (adapter == null) 
        {
             System.Console.WriteLine("❌ Failed to create adapter.");
             return;
        }

        var accountState = await adapter.GetAccountStateAsync();
        
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
