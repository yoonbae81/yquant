using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.App.OrderComposer;
using StackExchange.Redis;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using yQuant.Core.Ports.Output.Policies;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace yQuant.App.OrderComposer.Tests;

[TestClass]
public class OrderComposerTests
{
    private Mock<ILogger<Worker>>? _loggerMock;
    private Mock<IConfiguration>? _configMock;
    private Mock<IConnectionMultiplexer>? _redisMock;
    private Mock<ISubscriber>? _subscriberMock;
    private Mock<IDatabase>? _dbMock;
    private Mock<IServiceProvider>? _serviceProviderMock;
    private Mock<IPositionSizer>? _positionSizerMock;
    private Mock<IMarketRule>? _marketRuleMock;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<Worker>>();
        _configMock = new Mock<IConfiguration>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _subscriberMock = new Mock<ISubscriber>();
        _dbMock = new Mock<IDatabase>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _positionSizerMock = new Mock<IPositionSizer>();
        _marketRuleMock = new Mock<IMarketRule>();

        _redisMock.Setup(r => r.GetSubscriber(null)).Returns(_subscriberMock.Object);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        _serviceProviderMock.Setup(s => s.GetService(typeof(IPositionSizer))).Returns(_positionSizerMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IMarketRule))).Returns(_marketRuleMock.Object);

        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
    }

    [TestMethod]
    public async Task Worker_StartsAndStops_Successfully()
    {
        // Arrange
        var orderCompositionUseCaseMock = new Mock<yQuant.Core.Ports.Input.IOrderCompositionUseCase>();
        var worker = new Worker(_loggerMock!.Object, _redisMock!.Object, orderCompositionUseCaseMock.Object);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // No exceptions were thrown
    }
}