using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using yQuant.Core.Models;
using yQuant.App.OrderManager;
using StackExchange.Redis;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using yQuant.Core.Ports.Output.Policies;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace yQuant.App.OrderManager.Tests;

[TestClass]
public class OrderManagerTests
{
    private Mock<ILogger<Worker>>? _loggerMock;
    private Mock<IConfiguration>? _configMock;
    private Mock<IConnectionMultiplexer>? _redisMock;
    private Mock<ISubscriber>? _subscriberMock;
    private Mock<IDatabase>? _dbMock;
    private Mock<IServiceProvider>? _serviceProviderMock;
    private Mock<IPositionSizer>? _positionSizerMock;

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

        _redisMock.Setup(r => r.GetSubscriber(null)).Returns(_subscriberMock.Object);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        _serviceProviderMock.Setup(s => s.GetService(typeof(IPositionSizer))).Returns(_positionSizerMock.Object);

        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(ILoggerFactory))).Returns(loggerFactory.Object);
    }

    [TestMethod]
    public async Task Worker_StartsAndStops_Successfully()
    {
        // Arrange
        var orderCompositionUseCaseMock = new Mock<yQuant.Core.Ports.Input.IOrderCompositionUseCase>();
        var orderPublisherMock = new Mock<yQuant.Core.Ports.Output.Infrastructure.IOrderPublisher>();
        var scheduleExecutorLoggerMock = new Mock<ILogger<yQuant.App.OrderManager.Services.ScheduleExecutor>>();
        var scheduleExecutor = new yQuant.App.OrderManager.Services.ScheduleExecutor(
            scheduleExecutorLoggerMock.Object,
            _redisMock!.Object,
            orderPublisherMock.Object
        );

        // Setup IServiceScopeFactory mock
        var serviceScopeMock = new Mock<IServiceScope>();
        var scopeServiceProviderMock = new Mock<IServiceProvider>();
        scopeServiceProviderMock.Setup(sp => sp.GetService(typeof(yQuant.Core.Ports.Input.IOrderCompositionUseCase)))
            .Returns(orderCompositionUseCaseMock.Object);
        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(scopeServiceProviderMock.Object);

        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        serviceScopeFactoryMock.Setup(f => f.CreateScope()).Returns(serviceScopeMock.Object);

        var worker = new Worker(_loggerMock!.Object, _redisMock!.Object, serviceScopeFactoryMock.Object, scheduleExecutor);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // No exceptions were thrown
    }
}