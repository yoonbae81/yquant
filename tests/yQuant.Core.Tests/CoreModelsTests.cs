using Xunit;
using yQuant.Core.Models;
using System.Collections.Generic;

namespace yQuant.Core.Tests;

public class CoreModelsTests
{
    [Fact]
    public void Account_GetTotalEquity_CalculatesCorrectly()
    {
        // Arrange
        var account = new Account
        {
            Id = "TestAccount",
            AccountNumber = "12345",
            Broker = "TestBroker",
            Active = true,
            Deposits = new Dictionary<CurrencyType, decimal>
            {
                { CurrencyType.USD, 1000m }
            },
            Positions = new List<Position>
            {
                new Position
                {
                    AccountAlias = "TestAccount",
                    Ticker = "AAPL",
                    Currency = CurrencyType.USD,
                    Qty = 10,
                    AvgPrice = 150m,
                    CurrentPrice = 155m // PnL = (155-150)*10 = 50
                },
                new Position
                {
                    AccountAlias = "TestAccount",
                    Ticker = "GOOG",
                    Currency = CurrencyType.USD,
                    Qty = 5,
                    AvgPrice = 2800m,
                    CurrentPrice = 2850m // PnL = (2850-2800)*5 = 250
                }
            }
        };

        // Act
        // Total Equity = Deposit + (Position1 Qty * CurrentPrice) + (Position2 Qty * CurrentPrice)
        // Total Equity = 1000 + (10 * 155) + (5 * 2850) = 1000 + 1550 + 14250 = 16800
        var totalEquity = account.GetTotalEquity(CurrencyType.USD);

        // Assert
        Assert.Equal(16800m, totalEquity);
    }

    [Fact]
    public void Position_UnrealizedPnL_CalculatesCorrectly()
    {
        // Arrange
        var position = new Position
        {
            AccountAlias = "TestAccount",
            Ticker = "MSFT",
            Currency = CurrencyType.USD,
            Qty = 20,
            AvgPrice = 300m,
            CurrentPrice = 310m
        };

        // Act
        // PnL = (310 - 300) * 20 = 10 * 20 = 200
        var pnl = position.UnrealizedPnL;

        // Assert
        Assert.Equal(200m, pnl);
    }
}