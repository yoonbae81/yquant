using StackExchange.Redis;
using System.Threading.Tasks;

namespace yQuant.App.Console.Commands;

public class CheckAccountsCommand : ICommand
{
    private readonly IConnectionMultiplexer _redis;

    public CheckAccountsCommand(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public string Name => "checkaccounts";
    public string Description => "Checks the registered accounts in Valkey.";

    public async Task ExecuteAsync(string[] args)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync("broker:accounts");

        if (json.HasValue)
        {
            System.Console.WriteLine($"Found accounts in Valkey (broker:accounts): {json}");
        }
        else
        {
            System.Console.WriteLine("No accounts found in Valkey (broker:accounts key is empty).");
        }
    }
}
