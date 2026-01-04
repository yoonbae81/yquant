using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Valkey.Models;
using Order = yQuant.Core.Models.Order;

namespace yQuant.Infra.Valkey.Adapters
{
    public class ValkeyBrokerClient(IConnectionMultiplexer redis, string account) : IBrokerAdapter
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private string _account = account;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

        public Account Account => new Account
        {
            Alias = _account,
            Broker = "Valkey",
            Number = "N/A",
            AppKey = "N/A",
            AppSecret = "N/A",
            Deposits = [],
            Positions = new Dictionary<string, List<Position>>(),
            Active = true
        };

        public async Task<(bool Success, string Message)> PingAsync()
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.PingAsync();

                // Check if account exists in index
                if (await db.SetContainsAsync("account:index", _account))
                {
                    return (true, "Pong");
                }

                // If not found, try case-insensitive match
                var accounts = await db.SetMembersAsync("account:index");
                foreach (var acc in accounts)
                {
                    if (acc.ToString().Equals(_account, StringComparison.OrdinalIgnoreCase))
                    {
                        var foundAccount = acc.ToString();
                        System.Console.WriteLine($"Resolved account '{_account}' to '{foundAccount}'");
                        _account = foundAccount; // Update to correct casing
                        return (true, "Pong");
                    }
                }

                return (false, "Account not found in Valkey Index");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<Account> GetDepositAsync(CurrencyType? currency = null, bool forceRefresh = false)
        {
            // Direct Read from deposit:{account}
            var db = _redis.GetDatabase();
            var key = $"deposit:{_account}";
            var entries = await db.HashGetAllAsync(key);

            var deposits = new Dictionary<CurrencyType, decimal>();
            foreach (var entry in entries)
            {
                if (Enum.TryParse<CurrencyType>(entry.Name, out var c) && decimal.TryParse(entry.Value.ToString(), out var amount))
                {
                    if (currency == null || c == currency)
                    {
                        deposits[c] = amount;
                    }
                }
            }

            return new Account
            {
                Alias = _account,
                Number = "N/A",
                Broker = "Valkey",
                AppKey = "N/A",
                AppSecret = "N/A",
                Deposits = deposits,
                Active = true
            };
        }

        public async Task<List<Position>> GetPositionsAsync()
        {
            // Direct Read from position:{account}
            var db = _redis.GetDatabase();
            var key = $"position:{_account}";
            var entries = await db.HashGetAllAsync(key);

            var positions = new List<Position>();
            foreach (var entry in entries)
            {
                try
                {
                    var position = JsonSerializer.Deserialize<Position>(entry.Value.ToString());
                    if (position != null) positions.Add(position);
                }
                catch { /* Ignore */ }
            }
            return positions;
        }

        public Task<List<Position>> GetPositionsAsync(CountryCode country, bool forceRefresh = false)
        {
            // Valkey doesn't support filtering by country easily in Hash.
            // We fetch all and filter in memory.
            return GetPositionsAsync(); // Filtering logic should be in caller or we add it here if needed.
            // But Position model doesn't strictly have CountryCode, it has Exchange/Ticker.
            // For now, return all.
        }

        public async Task<PriceInfo> GetPriceAsync(string ticker)
        {
            // Direct Read from stock:{ticker}
            var db = _redis.GetDatabase();
            var key = $"stock:{ticker}";
            var priceVal = await db.HashGetAsync(key, "price");
            var changeVal = await db.HashGetAsync(key, "changeRate");

            if (priceVal.HasValue && decimal.TryParse(priceVal.ToString(), out var price))
            {
                decimal change = 0;
                if (changeVal.HasValue) decimal.TryParse(changeVal.ToString(), out change);

                return new PriceInfo(price, change);
            }

            throw new Exception($"Price not found for {ticker} in Valkey.");
        }

        public Task<PriceInfo> GetPriceAsync(string ticker, ExchangeCode exchange)
        {
            // Exchange doesn't matter for Valkey lookup as keys are by ticker
            return GetPriceAsync(ticker);
        }

        public async Task<OrderResult> PlaceOrderAsync(Order order)
        {
            var db = _redis.GetDatabase();
            var sub = _redis.GetSubscriber();
            var tcs = new TaskCompletionSource<OrderResult>();

            // Subscribe to execution channel to get the result
            // We need to filter by OrderId.
            // Note: This subscribes to ALL executions. In high volume, this might be inefficient.
            // But for Console/Dashboard usage, it's acceptable.
            // Ideally, we'd use a specific response channel, but the schema says 'execution' channel.

            await sub.SubscribeAsync(ValkeyChannel.Literal("execution"), (channel, message) =>
            {
                try
                {
                    var result = JsonSerializer.Deserialize<OrderResult>(message.ToString());
                    if (result != null && result.OrderId == order.Id.ToString())
                    {
                        tcs.TrySetResult(result);
                    }
                }
                catch { /* Ignore */ }
            });

            try
            {
                var orderJson = JsonSerializer.Serialize(order);
                await db.PublishAsync(ValkeyChannel.Literal("order"), orderJson);

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(_timeout));
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    return OrderResult.Failure("Order placed but no confirmation received (Timeout).");
                }
            }
            finally
            {
                await sub.UnsubscribeAsync(ValkeyChannel.Literal("execution"));
            }
        }

        public Task EnsureConnectedAsync() => Task.CompletedTask;
        public Task<IEnumerable<Order>> GetOpenOrdersAsync() => throw new NotImplementedException();
    }
}
