using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Models;
using Order = yQuant.Core.Models.Order;

namespace yQuant.Infra.Redis.Adapters
{
    public class RedisBrokerClient(IConnectionMultiplexer redis, string account) : IBrokerAdapter
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly string _account = account;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

        public Account Account => throw new NotImplementedException("Account object is not fully available in RedisClient. Use specific methods.");

        private async Task<T?> ExecuteRequestAsync<T>(BrokerRequestType type, string payload = "", bool forceRefresh = false)
        {
            var db = _redis.GetDatabase();
            var sub = _redis.GetSubscriber();
            var requestId = Guid.NewGuid();
            var responseChannel = $"broker:response:{requestId}";

            var request = new BrokerRequest
            {
                Id = requestId,
                Type = type,
                Account = _account,
                Payload = payload,
                ResponseChannel = responseChannel,
                ForceRefresh = forceRefresh
            };

            var tcs = new TaskCompletionSource<BrokerResponse>();

            await sub.SubscribeAsync(RedisChannel.Literal(responseChannel), (channel, message) =>
            {
                try
                {
                    var response = JsonSerializer.Deserialize<BrokerResponse>(message.ToString());
                    if (response != null)
                    {
                        tcs.TrySetResult(response!);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            try
            {
                await db.PublishAsync(RedisChannel.Literal("broker:requests"), JsonSerializer.Serialize(request));

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(_timeout));
                if (completedTask == tcs.Task)
                {
                    var response = await tcs.Task;
                    if (!response.Success)
                    {
                        throw new Exception(response.Message);
                    }

                    if (typeof(T) == typeof(bool)) return (T)(object)true; // For Ping
                    if (string.IsNullOrEmpty(response.Payload)) return default!;

                    return JsonSerializer.Deserialize<T>(response.Payload);
                }
                else
                {
                    throw new TimeoutException("Gateway did not respond in time.");
                }
            }
            finally
            {
                await sub.UnsubscribeAsync(RedisChannel.Literal(responseChannel));
            }
        }

        public async Task<(bool Success, string Message)> PingAsync()
        {
            try
            {
                // Timeout for Ping is 2s as per requirement
                var db = _redis.GetDatabase();
                var sub = _redis.GetSubscriber();
                var requestId = Guid.NewGuid();
                var responseChannel = $"broker:response:{requestId}";

                var request = new BrokerRequest
                {
                    Id = requestId,
                    Type = BrokerRequestType.Ping,
                    Account = _account,
                    ResponseChannel = responseChannel
                };

                var tcs = new TaskCompletionSource<BrokerResponse>();

                await sub.SubscribeAsync(RedisChannel.Literal(responseChannel), (channel, message) =>
                {
                    var response = JsonSerializer.Deserialize<BrokerResponse>(message.ToString());
                    if (response != null)
                    {
                        tcs.TrySetResult(response!);
                    }
                });

                await db.PublishAsync(RedisChannel.Literal("broker:requests"), JsonSerializer.Serialize(request));

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
                if (completedTask == tcs.Task)
                {
                    var response = await tcs.Task;
                    return (response.Success, response.Message);
                }
                return (false, "Gateway timed out.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<Account> GetDepositAsync(CurrencyType? currency = null, bool forceRefresh = false)
        {
            if (currency == null)
            {
                // Fetch full account state
                var account = await ExecuteRequestAsync<Account>(BrokerRequestType.GetDeposit, "", forceRefresh);
                return account ?? new Account
                {
                    Alias = _account,
                    Number = "N/A",
                    Broker = "Redis",
                    AppKey = "N/A",
                    AppSecret = "N/A",
                    Deposits = [],
                    Active = true
                };
            }
            else
            {
                // Fetch specific currency
                // Gateway returns the full Account object (serialized) even if we ask for specific currency
                var account = await ExecuteRequestAsync<Account>(BrokerRequestType.GetDeposit, currency.ToString()!, forceRefresh);

                if (account == null)
                {
                    return new Account
                    {
                        Number = "N/A",
                        Alias = _account,
                        Broker = "Redis",
                        AppKey = "N/A",
                        AppSecret = "N/A",
                        Deposits = [],
                        Active = true
                    };
                }
                return account;
            }
        }

        public async Task<List<Position>> GetPositionsAsync()
        {
            // Fetch all positions
            var result = await ExecuteRequestAsync<List<Position>>(BrokerRequestType.GetPositions, "", false);
            return result ?? [];
        }

        public async Task<List<Position>> GetPositionsAsync(CountryCode country, bool forceRefresh = false)
        {
            var result = await ExecuteRequestAsync<List<Position>>(BrokerRequestType.GetPositions, country.ToString(), forceRefresh);
            return result ?? [];
        }

        public async Task<PriceInfo> GetPriceAsync(string ticker)
        {
            var result = await ExecuteRequestAsync<PriceInfo>(BrokerRequestType.GetPrice, ticker);
            return result ?? throw new Exception("Failed to get price info from gateway.");
        }

        public async Task<OrderResult> PlaceOrderAsync(Order order)
        {
            try
            {
                var result = await ExecuteRequestAsync<OrderResult>(BrokerRequestType.PlaceOrder, JsonSerializer.Serialize(order));
                return result ?? OrderResult.Failure("Gateway did not respond.");
            }
            catch (Exception ex)
            {
                return OrderResult.Failure(ex.Message);
            }
        }

        // Unused methods from interface
        public Task EnsureConnectedAsync() => Task.CompletedTask;
        public Task<IEnumerable<Order>> GetOpenOrdersAsync() => throw new NotImplementedException();
    }
}
