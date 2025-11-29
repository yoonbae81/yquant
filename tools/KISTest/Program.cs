using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Middleware.Redis;
using yQuant.Infra.Middleware.Redis.Interfaces;
using yQuant.Infra.Middleware.Redis.Services;

Console.WriteLine("🔍 KIS Account Connection Test");
Console.WriteLine("================================\n");

// Parse command-line arguments
bool refreshToken = args.Contains("--refresh-token") || args.Contains("-r");
if (refreshToken)
{
    Console.WriteLine("⚠️  Token refresh mode enabled - will invalidate existing token\n");
}

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Get Account:0 configuration
var accounts = configuration.GetSection("Accounts").Get<List<AccountConfig>>();
if (accounts == null || accounts.Count == 0)
{
    Console.WriteLine("❌ No accounts configured!");
    Console.WriteLine("\n💡 Configure Account:0 using User Secrets:");
    Console.WriteLine("dotnet user-secrets set \"Accounts:0:UserId\" \"YOUR_USER_ID\"");
    Console.WriteLine("dotnet user-secrets set \"Accounts:0:Credentials:AppKey\" \"YOUR_APP_KEY\"");
    Console.WriteLine("dotnet user-secrets set \"Accounts:0:Credentials:AppSecret\" \"YOUR_APP_SECRET\"");
    Environment.Exit(1);
}

var account = accounts[0];
Console.WriteLine($"📋 Account Info:");
Console.WriteLine($"   Alias: {account.Alias}");
Console.WriteLine($"   UserId: {account.UserId}");
Console.WriteLine($"   AccountNumber: {account.AccountNumber}");
Console.WriteLine($"   BrokerType: {account.BrokerType}");
Console.WriteLine($"   BaseUrl: {account.Credentials?.BaseUrl}\n");

if (string.IsNullOrEmpty(account.Credentials?.AppKey) || 
    account.Credentials.AppKey == "YOUR_APP_KEY_1")
{
    Console.WriteLine("❌ AppKey not configured!");
    Console.WriteLine("\n💡 Set your real credentials using User Secrets:");
    Console.WriteLine("dotnet user-secrets set \"Accounts:0:Credentials:AppKey\" \"YOUR_REAL_APP_KEY\"");
    Console.WriteLine("dotnet user-secrets set \"Accounts:0:Credentials:AppSecret\" \"YOUR_REAL_APP_SECRET\"");
    Environment.Exit(1);
}

try
{
    // Create logger
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });
    var logger = loggerFactory.CreateLogger<KisConnector>();

    // Redis Service
    IRedisService? redisService = null;
    var redisHost = Environment.GetEnvironmentVariable("Redis__Host");
    if (!string.IsNullOrEmpty(redisHost))
    {
        var redisPort = Environment.GetEnvironmentVariable("Redis__Port") ?? "6379";
        var redisUser = Environment.GetEnvironmentVariable("Redis__User");
        var redisPass = Environment.GetEnvironmentVariable("Redis__Passwd");

        var connectionString = $"{redisHost}:{redisPort}";
        if (!string.IsNullOrEmpty(redisPass)) connectionString += $",password={redisPass}";
        if (!string.IsNullOrEmpty(redisUser)) connectionString += $",user={redisUser}";
        
        try 
        {
            var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString);
            var redisLogger = loggerFactory.CreateLogger<RedisService>();
            redisService = new RedisService(multiplexer, redisLogger);
            Console.WriteLine("✅ Redis connected for token caching");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Redis connection failed: {ex.Message}. Proceeding without Redis.");
        }
    }

    // Create HTTP client
    var httpClient = new HttpClient();

    Console.WriteLine("Test 1: Creating KIS Client...");
    var kisClient = new KisConnector(
        httpClient,
        logger,
        redisService,
        account.UserId!,
        account.Alias!,
        account.Credentials!.AppKey!,
        account.Credentials.AppSecret!,
        account.Credentials.BaseUrl!
    );
    Console.WriteLine("✅ KIS Client created\n");

    // Invalidate existing token if refresh mode is enabled
    if (refreshToken)
    {
        Console.WriteLine("Test 1.5: Invalidating existing token...");
        await kisClient.InvalidateTokenAsync();
        Console.WriteLine("✅ Token invalidated\n");
    }

    Console.WriteLine("Test 2: Connecting to KIS API (getting access token)...");
    await kisClient.EnsureConnectedAsync();
    Console.WriteLine("✅ Successfully connected to KIS API!\n");

    Console.WriteLine("Test 3: Getting account balance...");
    var adapter = new KisBrokerAdapter(
        loggerFactory.CreateLogger<KisBrokerAdapter>(),
        kisClient,
        account.AccountNumber!.Split('-')[0],
        account.UserId!,
        account.Alias
    );

    var accountState = await adapter.GetAccountStateAsync(account.AccountNumber!);
    Console.WriteLine($"✅ Account retrieved:");
    Console.WriteLine($"   Account ID: {accountState.Id}");
    Console.WriteLine($"   Broker: {accountState.Broker}");
    Console.WriteLine($"   Active: {accountState.Active}");
    Console.WriteLine($"   Deposits: {accountState.Deposits.Count} currencies\n");

    foreach (var deposit in accountState.Deposits)
    {
        Console.WriteLine($"   {deposit.Key}: {deposit.Value:N0}");
    }

    Console.WriteLine("\n================================");
    Console.WriteLine("✅ All tests passed!");
    Console.WriteLine($"KIS Account '{account.Alias}' is ready!");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"❌ HTTP Request failed: {ex.Message}");
    Console.WriteLine("\n💡 Troubleshooting:");
    Console.WriteLine("1. Check your internet connection");
    Console.WriteLine("2. Verify BaseUrl is correct (실전투자 vs 모의투자)");
    Console.WriteLine("3. Check if KIS API is accessible");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"\nStack trace: {ex.StackTrace}");
    Console.WriteLine("\n💡 Troubleshooting:");
    Console.WriteLine("1. Verify AppKey and AppSecret are correct");
    Console.WriteLine("2. Check if the app is activated in KIS OpenAPI portal");
    Console.WriteLine("3. Ensure UserId matches the account");
    Environment.Exit(1);
}

// Account configuration class
public class AccountConfig
{
    public string? Alias { get; set; }
    public string? UserId { get; set; }
    public string? BrokerType { get; set; }
    public string? AccountNumber { get; set; }
    public CredentialsConfig? Credentials { get; set; }
}

public class CredentialsConfig
{
    public string? AppKey { get; set; }
    public string? AppSecret { get; set; }
    public string? BaseUrl { get; set; }
}
