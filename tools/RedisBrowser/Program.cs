using StackExchange.Redis;
using System;
using System.Net;


var connectionString = Environment.GetEnvironmentVariable("Redis");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("❌ Error: 'Redis' environment variable is not set.");
    Environment.Exit(1);
}

var options = ConfigurationOptions.Parse(connectionString);
options.AbortOnConnectFail = false;

var redis = ConnectionMultiplexer.Connect(options);
var db = redis.GetDatabase();
var endpoint = redis.GetEndPoints().First();
var server = redis.GetServer(endpoint);

Console.WriteLine("Connected to Redis.");

var keys = new List<RedisKey>();
for(int i=0; i<10; i++)
{
    var key = await db.KeyRandomAsync();
    if (key != default(RedisKey)) keys.Add(key);
}

if (keys.Count == 0)
{
    Console.WriteLine("No keys found matching 'cache:master:*'");
}
else
{
    Console.WriteLine($"Found {keys.Count} keys. Showing values:");
    foreach (var key in keys)
    {
        var hash = await db.HashGetAllAsync(key);
        Console.WriteLine($"Key: {key}");
        foreach (var entry in hash)
        {
            Console.WriteLine($"  {entry.Name}: {entry.Value}");
        }
        Console.WriteLine();
    }
}
