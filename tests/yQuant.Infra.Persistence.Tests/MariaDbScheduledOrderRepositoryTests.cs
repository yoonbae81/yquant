using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using yQuant.Core.Models;
using yQuant.Infra.Persistence;

namespace yQuant.Infra.Persistence.Tests;

[TestClass]
public class MariaDbScheduledOrderRepositoryTests
{
    private MariaDbContext _context = null!;
    private Mock<ILogger<MariaDbScheduledOrderRepository>> _loggerMock = null!;
    private MariaDbScheduledOrderRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<MariaDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new MariaDbContext(options);
        _loggerMock = new Mock<ILogger<MariaDbScheduledOrderRepository>>();
        _repository = new MariaDbScheduledOrderRepository(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public void Constructor_WithValidContext_InitializesCorrectly()
    {
        // Assert
        Assert.IsNotNull(_repository);
    }

    [TestMethod]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmpty()
    {
        // Act
        var orders = await _repository.GetAllAsync("TestAccount");

        // Assert
        Assert.AreEqual(0, orders.Count());
    }

    // Note: AddOrUpdateAsync, RemoveAsync, and ProcessOrdersAsync use ExecuteUpdateAsync
    // which is not supported by in-memory database. These methods will be tested
    // with integration tests against actual MariaDB instance.
}
