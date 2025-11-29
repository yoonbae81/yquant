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

// Load .env files
var root = Directory.GetCurrentDirectory();
var dotenv = Path.Combine(root, ".env");
var dotenvLocal = Path.Combine(root, ".env.local");

// Try to find .env in parent directories if not found in current (common in dev)
if (!File.Exists(dotenv) && !File.Exists(dotenvLocal))
{
    var parent = Directory.GetParent(root);
    while (parent != null)
    {
        var pEnv = Path.Combine(parent.FullName, ".env");
        var pEnvLocal = Path.Combine(parent.FullName, ".env.local");
        if (File.Exists(pEnv) || File.Exists(pEnvLocal))
        {
            root = parent.FullName;
            dotenv = Path.Combine(root, ".env");
            dotenvLocal = Path.Combine(root, ".env.local");
            break;
        }
        parent = parent.Parent;
    }
}

LoadEnvFile(dotenv);
LoadEnvFile(dotenvLocal);

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables()
    .Build();

static void LoadEnvFile(string filePath)
{
    if (!File.Exists(filePath)) return;

    Console.WriteLine($"📄 Loading env file: {filePath}");
    foreach (var line in File.ReadAllLines(filePath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

        var parts = line.Split('=', 2);
        if (parts.Length != 2) continue;

        var key = parts[0].Trim();
        var value = parts[1].Trim();
        
        // Handle double quotes
        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
        {
            value = value.Substring(1, value.Length - 2);
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

// Get Account configuration from Environment Variables
var account = new AccountConfig
{
    AppKey = configuration["KIS__AppKey"],
    AppSecret = configuration["KIS__AppSecret"],
    Number = configuration["KIS__AccountNumber"],
    Alias = configuration["KIS__Alias"] ?? "MainAccount",
    Broker = configuration["KIS__Broker"] ?? "KIS",
};

if (string.IsNullOrEmpty(account.AppKey))
{
    // Fallback to User Secrets "Accounts:0" for backward compatibility or if user prefers secrets
    var legacyAccount = configuration.GetSection("Accounts").Get<List<AccountConfig>>()?.FirstOrDefault();
    if (legacyAccount != null)
    {
        account = legacyAccount;
    }
}

Console.WriteLine($"📋 Account Info:");
Console.WriteLine($"   Alias: {account.Alias}");
Console.WriteLine($"   Number: {account.Number}");
Console.WriteLine($"   Broker: {account.Broker}");

if (string.IsNullOrEmpty(account.AppKey) || 
    account.AppKey == "YOUR_APP_KEY_1")
{
    Console.WriteLine("❌ AppKey not configured!");
    Console.WriteLine("\n💡 Set your credentials in .env.local:");
    Console.WriteLine("KIS__AppKey=YOUR_APP_KEY");
    Console.WriteLine("KIS__AppSecret=YOUR_APP_SECRET");
    Console.WriteLine("KIS__AccountNumber=12345678-01");
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

    // Load API Config
    var apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));

    Console.WriteLine("Test 1: Creating KIS Client...");
    var kisClient = new KisConnector(
        httpClient,
        logger,
        redisService,
        account.Alias!,
        account.AppKey!,
        account.AppSecret!,
        apiConfig
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
        account.Number!.Split('-')[0],
        account.Alias
    );

    var accountState = await adapter.GetAccountStateAsync(account.Number!);
    Console.WriteLine($"✅ Account retrieved:");
    Console.WriteLine($"   Account ID: {accountState.Alias}");
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
    Console.WriteLine("2. Check if KIS API is accessible");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"\nStack trace: {ex.StackTrace}");
    Console.WriteLine("\n💡 Troubleshooting:");
    Console.WriteLine("1. Verify AppKey and AppSecret are correct");
    Console.WriteLine("2. Check if the app is activated in KIS OpenAPI portal");
    Console.WriteLine("3. Ensure account alias is correct");
    Environment.Exit(1);
}

// Account configuration class
public class AccountConfig
{
    public string? Alias { get; set; }
    public string? Broker { get; set; }
    public string? Number { get; set; }
    public string? AppKey { get; set; }
    public string? AppSecret { get; set; }
}
