using Microsoft.Extensions.Configuration;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Collections.Generic;
using System;
using yQuant.App.OrderManager.Adapters;
using yQuant.Core.Ports.Output.Policies;

namespace yQuant.App.OrderManager.Tests;

[TestClass]
public class PolicyMappingTests
{
    [TestMethod]
    public void Constructor_ShouldSucceed_WhenAllPoliciesExist()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"OrderManager:StrategySizingMapping:Strat1", "Basic"},
            {"OrderManager:StrategySizingMapping:Strat2", "OnlyOne"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var sizers = new List<IPositionSizer> { new BasicSizerStub(), new OnlyOneSizerStub() };

        // Act
        var mapper = new ConfigStrategyPolicyMapper(configuration, sizers);

        // Assert
        Assert.AreEqual("Basic", mapper.GetSizingPolicyName("Strat1"));
        Assert.AreEqual("OnlyOne", mapper.GetSizingPolicyName("Strat2"));
    }

    [TestMethod]
    public void Constructor_ShouldThrow_WhenPolicyMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"OrderManager:StrategySizingMapping:Strat1", "NonExistent"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var sizers = new List<IPositionSizer> { new BasicSizerStub() };

        // Act & Assert
        try
        {
            new ConfigStrategyPolicyMapper(configuration, sizers);
            Assert.Fail("Should have thrown InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(ex.Message.Contains("NonExistent"));
        }
    }

    // Stubs for GetType().Name matching
    private class BasicSizerStub : IPositionSizer
    {
        public yQuant.Core.Models.Order? CalculatePositionSize(yQuant.Core.Models.Signal signal, yQuant.Core.Models.Account account) => null;
        public bool ValidateOrder(yQuant.Core.Models.Order order, yQuant.Core.Models.Account account, out string failureReason) { failureReason = ""; return true; }
    }
    private class OnlyOneSizerStub : IPositionSizer
    {
        public yQuant.Core.Models.Order? CalculatePositionSize(yQuant.Core.Models.Signal signal, yQuant.Core.Models.Account account) => null;
        public bool ValidateOrder(yQuant.Core.Models.Order order, yQuant.Core.Models.Account account, out string failureReason) { failureReason = ""; return true; }
    }
}
