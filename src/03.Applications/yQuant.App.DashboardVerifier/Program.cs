using StackExchange.Redis;
using System.Text.Json;

Console.WriteLine("Starting Dashboard Verifier...");

try
{
    var redis = ConnectionMultiplexer.Connect("localhost");
    var db = redis.GetDatabase();

    Console.WriteLine("Connected to Redis.");

    // Test 1: Save Order
    var order = new
    {
        Id = Guid.NewGuid(),
        AccountAlias = "TestAccount",
        Ticker = "AAPL",
        Action = 0, // Buy
        ScheduledTime = DateTime.UtcNow.AddMinutes(1),
        IsActive = true,
        Account = "TestAccount" // Extra field just in case
    };
    var list = new List<object> { order };
    var jsonToSave = JsonSerializer.Serialize(list);
    await db.StringSetAsync("dashboard:scheduled_orders", jsonToSave);
    Console.WriteLine("Saved order to Redis: " + jsonToSave);

    // Test 2: Load Order
    var jsonLoaded = await db.StringGetAsync("dashboard:scheduled_orders");
    Console.WriteLine($"Loaded from Redis: {jsonLoaded}");

    if (jsonLoaded == jsonToSave)
    {
        Console.WriteLine("SUCCESS: Redis persistence verified.");
    }
    else
    {
        Console.WriteLine("FAILURE: Redis persistence mismatch.");
    }

    // Test 2.5: Check Available Accounts
    var accountsJson = await db.StringGetAsync("broker:available_accounts");
    Console.WriteLine($"Available Accounts in Redis: {accountsJson}");
    if (accountsJson.HasValue)
    {
        Console.WriteLine("SUCCESS: Accounts found.");
    }
    else
    {
        Console.WriteLine("FAILURE: No accounts found.");
    }

    // Test 3: Publish Request
    var sub = redis.GetSubscriber();
    await sub.SubscribeAsync("broker:requests", (channel, message) =>
    {
        Console.WriteLine($"Received on broker:requests: {message}");
    });

    var request = new
    {
        Id = Guid.NewGuid(),
        Type = "PlaceOrder",
        Account = "TestAccount",
        Payload = "{}"
    };
    await db.PublishAsync("broker:requests", JsonSerializer.Serialize(request));
    Console.WriteLine("Published request to broker:requests.");

    // Wait for message
    await Task.Delay(2000);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}
