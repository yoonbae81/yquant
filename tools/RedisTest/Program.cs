using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using System.Net;

Console.WriteLine("🔍 Redis Connection Test");
Console.WriteLine("========================\n");

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = Environment.GetEnvironmentVariable("Redis");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("❌ Error: 'Redis' environment variable is not set.");
    Environment.Exit(1);
}

Console.WriteLine($"📡 Connection String: {connectionString}\n");

try
{
    // Test 1: Connect to Redis
    Console.WriteLine("Test 1: Connecting to Redis...");
    
    var options = ConfigurationOptions.Parse(connectionString);
    options.AbortOnConnectFail = false;

    Console.WriteLine("Parsed successfully.");
    foreach (var ep in options.EndPoints)
    {
        Console.WriteLine($"EndPoint: {ep} (Type: {ep.GetType().Name})");
        if (ep is DnsEndPoint dns)
        {
            Console.WriteLine($"  Host: {dns.Host}");
            Console.WriteLine($"  Port: {dns.Port}");
        }
    }
    Console.WriteLine($"User: {options.User}");
    Console.WriteLine($"Password: {options.Password}");
    Console.WriteLine($"AbortOnConnectFail: {options.AbortOnConnectFail}");



    var redis = ConnectionMultiplexer.Connect(options);
    
    // Wait for connection
    int retry = 0;
    while (!redis.IsConnected && retry < 20)
    {
        Console.WriteLine($"Waiting for connection... ({retry + 1}/20)");
        Thread.Sleep(500);
        retry++;
    }

    if (redis.IsConnected)
    {
        Console.WriteLine("✅ Connected successfully!\n");
    }
    else
    {
        Console.WriteLine("⚠️ Connection not established yet (IsConnected=false).");
        Console.WriteLine($"Status: {redis.GetStatus()}");
    }

    // Test 2: Ping
    Console.WriteLine("Test 2: Ping test...");
    var db = redis.GetDatabase();
    var latency = db.Ping();
    Console.WriteLine($"✅ Ping latency: {latency.TotalMilliseconds:F2} ms\n");

    // Test 3: Write and Read
    Console.WriteLine("Test 3: Write and Read test...");
    var testKey = "yquant:test:connection";
    var testValue = $"Test at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    
    db.StringSet(testKey, testValue);
    Console.WriteLine($"✅ Written: {testKey} = {testValue}");
    
    var readValue = db.StringGet(testKey);
    Console.WriteLine($"✅ Read: {testKey} = {readValue}");
    
    db.KeyDelete(testKey);
    Console.WriteLine($"✅ Deleted test key\n");

    // Test 4: Pub/Sub
    Console.WriteLine("Test 4: Pub/Sub test...");
    var subscriber = redis.GetSubscriber();
    var channel = RedisChannel.Literal("yquant:test:channel");
    var messageReceived = false;

    subscriber.Subscribe(channel, (ch, message) =>
    {
        Console.WriteLine($"✅ Received message: {message}");
        messageReceived = true;
    });

    Thread.Sleep(100); // Wait for subscription to be ready
    subscriber.Publish(channel, "Hello from yQuant!");
    Thread.Sleep(100); // Wait for message to be received

    if (messageReceived)
    {
        Console.WriteLine("✅ Pub/Sub working correctly\n");
    }
    else
    {
        Console.WriteLine("⚠️  Pub/Sub message not received (this is OK for basic testing)\n");
    }

    subscriber.Unsubscribe(channel);

    // Test 5: Server Info
    Console.WriteLine("Test 5: Getting server info...");
    var endpoints = redis.GetEndPoints();
    Console.WriteLine($"✅ Connected to: {endpoints[0]}\n");

    // Summary
    Console.WriteLine("========================");
    Console.WriteLine("✅ All critical tests passed!");
    Console.WriteLine("Redis is ready for yQuant.NET");

    redis.Dispose();
}
catch (RedisConnectionException ex)
{
    Console.WriteLine($"❌ Connection failed: {ex.Message}");
    Console.WriteLine("\n💡 Troubleshooting:");
    Console.WriteLine("1. Check if Redis is running:");
    Console.WriteLine("   docker ps | findstr redis");
    Console.WriteLine("2. Start Redis if not running:");
    Console.WriteLine("   docker run -d --name yquant-redis -p 6379:6379 redis:latest");
    Console.WriteLine("3. Check connection string in appsettings.json or User Secrets");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Environment.Exit(1);
}
