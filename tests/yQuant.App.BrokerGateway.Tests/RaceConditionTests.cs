using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.App.BrokerGateway.Tests;

/// <summary>
/// Tests for race condition prevention in position and deposit updates.
/// These tests verify that concurrent order processing doesn't cause Lost Update problems.
/// </summary>
[TestClass]
[Ignore("These tests require local Redis for reliable concurrent testing. Cloud Redis has network latency that makes these tests unreliable.")]
public class RaceConditionTests
{
    private IConnectionMultiplexer? _redis;
    private IDatabase? _db;
    private const string TestAccountAlias = "TestAccount";
    private const string TestTicker = "AAPL";

    [TestInitialize]
    public async Task TestInitialize()
    {
        try
        {
            // Connect to Redis Labs cloud instance for testing
            _redis = await ConnectionMultiplexer.ConnectAsync("redis-12848.c340.ap-northeast-2-1.ec2.cloud.redislabs.com:12848,password=pTr1iIWzFNfLiExY9t0IXLw5hVWDnLpg");
            _db = _redis.GetDatabase();

            // Clean up test data
            await CleanupTestDataAsync();
        }
        catch (RedisConnectionException)
        {
            Assert.Inconclusive("Redis server is not available. Please check the connection string.");
        }
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_db != null)
        {
            await CleanupTestDataAsync();
        }

        _redis?.Dispose();
    }

    private async Task CleanupTestDataAsync()
    {
        if (_db == null) return;

        await _db.KeyDeleteAsync($"position:{TestAccountAlias}");
        await _db.KeyDeleteAsync($"deposit:{TestAccountAlias}");
        await _db.KeyDeleteAsync($"stock:{TestTicker}");
    }

    [TestMethod]
    public async Task UpdatePosition_ConcurrentBuyOrders_ShouldNotLoseUpdates()
    {
        // Arrange
        var initialPrice = 100m;
        var orderQty = 10m;
        var numberOfConcurrentOrders = 10;

        // Set initial stock price
        await _db!.HashSetAsync($"stock:{TestTicker}", "price", initialPrice.ToString());

        // Create multiple concurrent buy orders
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfConcurrentOrders; i++)
        {
            var price = initialPrice + i; // Slightly different prices
            tasks.Add(ExecutePositionUpdateAsync(OrderAction.Buy, orderQty, price));
        }

        // Act - Execute all orders concurrently
        await Task.WhenAll(tasks);

        // Assert
        var posJson = await _db.HashGetAsync($"position:{TestAccountAlias}", TestTicker);
        Assert.IsTrue(posJson.HasValue, "Position should exist after buy orders");

        var position = JsonSerializer.Deserialize<Position>(posJson.ToString());
        Assert.IsNotNull(position);

        // Expected total quantity
        var expectedQty = orderQty * numberOfConcurrentOrders;
        Assert.AreEqual(expectedQty, position.Qty,
            $"Position quantity should be {expectedQty} (no lost updates)");

        // Expected average price calculation
        decimal totalCost = 0;
        for (int i = 0; i < numberOfConcurrentOrders; i++)
        {
            totalCost += (initialPrice + i) * orderQty;
        }
        var expectedAvgPrice = totalCost / expectedQty;

        Assert.AreEqual(expectedAvgPrice, position.AvgPrice, 0.01m,
            "Average price should be calculated correctly");
    }

    [TestMethod]
    public async Task UpdatePosition_ConcurrentPartialFills_ShouldNotLoseUpdates()
    {
        // Arrange - Simulate partial fills of a large order
        var price = 150m;
        var partialFillQty = 5m;
        var numberOfPartialFills = 20; // 20 partial fills happening rapidly

        // Act - Execute all partial fills concurrently
        var tasks = Enumerable.Range(0, numberOfPartialFills)
            .Select(_ => ExecutePositionUpdateAsync(OrderAction.Buy, partialFillQty, price))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        var posJson = await _db!.HashGetAsync($"position:{TestAccountAlias}", TestTicker);
        Assert.IsTrue(posJson.HasValue);

        var position = JsonSerializer.Deserialize<Position>(posJson.ToString());
        Assert.IsNotNull(position);

        var expectedQty = partialFillQty * numberOfPartialFills;
        Assert.AreEqual(expectedQty, position.Qty,
            "All partial fills should be accounted for (no lost updates)");
        Assert.AreEqual(price, position.AvgPrice,
            "Average price should be consistent for same-price fills");
    }

    [TestMethod]
    public async Task UpdatePosition_ConcurrentBuyAndSell_ShouldMaintainCorrectQuantity()
    {
        // Arrange - Set up initial position
        var initialQty = 100m;
        var price = 100m;
        await SetupInitialPositionAsync(initialQty, price);

        // Act - Execute concurrent buy and sell orders
        var buyQty = 10m;
        var sellQty = 5m;
        var numberOfBuys = 5;
        var numberOfSells = 10;

        var tasks = new List<Task>();

        // Add buy tasks
        for (int i = 0; i < numberOfBuys; i++)
        {
            tasks.Add(ExecutePositionUpdateAsync(OrderAction.Buy, buyQty, price + i));
        }

        // Add sell tasks
        for (int i = 0; i < numberOfSells; i++)
        {
            tasks.Add(ExecutePositionUpdateAsync(OrderAction.Sell, sellQty, price));
        }

        await Task.WhenAll(tasks);

        // Assert
        var posJson = await _db!.HashGetAsync($"position:{TestAccountAlias}", TestTicker);
        Assert.IsTrue(posJson.HasValue);

        var position = JsonSerializer.Deserialize<Position>(posJson.ToString());
        Assert.IsNotNull(position);

        var expectedQty = initialQty + (buyQty * numberOfBuys) - (sellQty * numberOfSells);
        Assert.AreEqual(expectedQty, position.Qty,
            "Final quantity should reflect all buys and sells correctly");
    }

    [TestMethod]
    public async Task UpdatePosition_SellToZero_ShouldDeletePosition()
    {
        // Arrange - Set up initial position
        var initialQty = 50m;
        var price = 100m;
        await SetupInitialPositionAsync(initialQty, price);

        // Act - Sell all shares concurrently (simulating multiple partial sells)
        var sellQty = 10m;
        var numberOfSells = 5; // 5 x 10 = 50 (exactly the initial quantity)

        var tasks = Enumerable.Range(0, numberOfSells)
            .Select(_ => ExecutePositionUpdateAsync(OrderAction.Sell, sellQty, price))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        var posExists = await _db!.HashExistsAsync($"position:{TestAccountAlias}", TestTicker);
        Assert.IsFalse(posExists, "Position should be deleted when quantity reaches zero");
    }

    [TestMethod]
    public async Task UpdateDeposit_ConcurrentBuyOrders_ShouldNotLoseUpdates()
    {
        // Arrange
        var initialBalance = 10000m;
        await _db!.HashSetAsync($"deposit:{TestAccountAlias}", "USD", initialBalance.ToString());

        var orderPrice = 100m;
        var orderQty = 10m;
        var numberOfOrders = 10;

        // Act - Execute concurrent buy orders (each should decrease balance)
        var tasks = Enumerable.Range(0, numberOfOrders)
            .Select(_ => ExecuteDepositUpdateAsync(OrderAction.Buy, orderQty, orderPrice, "USD"))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        var balanceStr = await _db.HashGetAsync($"deposit:{TestAccountAlias}", "USD");
        Assert.IsTrue(balanceStr.HasValue);

        var finalBalance = decimal.Parse(balanceStr.ToString());
        var expectedBalance = initialBalance - (orderPrice * orderQty * numberOfOrders);

        Assert.AreEqual(expectedBalance, finalBalance,
            "Balance should reflect all buy orders (no lost updates)");
    }

    [TestMethod]
    public async Task UpdateDeposit_ConcurrentBuyAndSell_ShouldMaintainCorrectBalance()
    {
        // Arrange
        var initialBalance = 10000m;
        await _db!.HashSetAsync($"deposit:{TestAccountAlias}", "USD", initialBalance.ToString());

        var price = 100m;
        var buyQty = 10m;
        var sellQty = 5m;
        var numberOfBuys = 5;
        var numberOfSells = 10;

        // Act - Execute concurrent buy and sell orders
        var tasks = new List<Task>();

        for (int i = 0; i < numberOfBuys; i++)
        {
            tasks.Add(ExecuteDepositUpdateAsync(OrderAction.Buy, buyQty, price, "USD"));
        }

        for (int i = 0; i < numberOfSells; i++)
        {
            tasks.Add(ExecuteDepositUpdateAsync(OrderAction.Sell, sellQty, price, "USD"));
        }

        await Task.WhenAll(tasks);

        // Assert
        var balanceStr = await _db.HashGetAsync($"deposit:{TestAccountAlias}", "USD");
        var finalBalance = decimal.Parse(balanceStr.ToString());

        var expectedBalance = initialBalance
            - (price * buyQty * numberOfBuys)  // Buys decrease balance
            + (price * sellQty * numberOfSells); // Sells increase balance

        Assert.AreEqual(expectedBalance, finalBalance,
            "Balance should reflect all transactions correctly");
    }

    [TestMethod]
    public async Task UpdatePosition_StressTest_100ConcurrentOrders()
    {
        // Arrange - Stress test with many concurrent operations
        var numberOfOrders = 100;
        var random = new Random(42); // Fixed seed for reproducibility

        // Act
        var tasks = new List<Task>();

        for (int i = 0; i < numberOfOrders; i++)
        {
            var action = random.Next(2) == 0 ? OrderAction.Buy : OrderAction.Sell;
            var qty = random.Next(1, 11); // 1-10 shares
            var price = 100m + random.Next(-10, 11); // 90-110

            tasks.Add(ExecutePositionUpdateAsync(action, qty, price));
        }

        // Assert - Main goal is to ensure no exceptions and data integrity
        await Task.WhenAll(tasks);

        // Verify position exists or doesn't exist (depending on final qty)
        var posJson = await _db!.HashGetAsync($"position:{TestAccountAlias}", TestTicker);

        // If position exists, verify it's valid JSON and has reasonable values
        if (posJson.HasValue)
        {
            var position = JsonSerializer.Deserialize<Position>(posJson.ToString());
            Assert.IsNotNull(position);
            Assert.IsGreaterThan(position.Qty, 0m, "If position exists, quantity should be positive");
            Assert.IsGreaterThan(position.AvgPrice, 0m, "If position exists, average price should be positive");
        }
    }

    // Helper methods

    private async Task ExecutePositionUpdateAsync(OrderAction action, decimal qty, decimal price)
    {
        var result = await _db!.ScriptEvaluateAsync(
            RedisLuaScripts.UpdatePositionScript,
            new RedisKey[] { $"position:{TestAccountAlias}" },
            new RedisValue[]
            {
                TestTicker,                    // ARGV[1]
                action.ToString(),             // ARGV[2]
                qty.ToString(),                // ARGV[3]
                price.ToString(),              // ARGV[4]
                TestAccountAlias,              // ARGV[5]
                "USD",                         // ARGV[6]
                price.ToString()               // ARGV[7]
            }
        );
    }

    private async Task ExecuteDepositUpdateAsync(OrderAction action, decimal qty, decimal price, string currency)
    {
        var amountChange = qty * price;

        var result = await _db!.ScriptEvaluateAsync(
            RedisLuaScripts.UpdateDepositScript,
            new RedisKey[] { $"deposit:{TestAccountAlias}" },
            new RedisValue[]
            {
                currency,                      // ARGV[1]
                action.ToString(),             // ARGV[2]
                amountChange.ToString()        // ARGV[3]
            }
        );
    }

    private async Task SetupInitialPositionAsync(decimal qty, decimal avgPrice)
    {
        var position = new Position
        {
            AccountAlias = TestAccountAlias,
            Ticker = TestTicker,
            Currency = CurrencyType.USD,
            Qty = qty,
            AvgPrice = avgPrice,
            CurrentPrice = avgPrice
        };

        await _db!.HashSetAsync(
            $"position:{TestAccountAlias}",
            TestTicker,
            JsonSerializer.Serialize(position)
        );
    }
}
