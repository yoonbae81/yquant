using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using yQuant.Core.Models;
using yQuant.Infra.Persistence;

namespace yQuant.Infra.Persistence.Tests;

[TestClass]
public class MariaDbDailySnapshotRepositoryTests
{
    private MariaDbContext _context = null!;
    private Mock<ILogger<MariaDbDailySnapshotRepository>> _loggerMock = null!;
    private MariaDbDailySnapshotRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<MariaDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new MariaDbContext(options);
        _loggerMock = new Mock<ILogger<MariaDbDailySnapshotRepository>>();
        _repository = new MariaDbDailySnapshotRepository(_context, _loggerMock.Object);
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
    public async Task SaveAsync_NewSnapshot_SavesSuccessfully()
    {
        // Arrange
        var snapshot = new DailySnapshot
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Currency = CurrencyType.KRW,
            TotalEquity = 1000000m,
            CashBalance = 500000m,
            PositionValue = 500000m,
            DailyPnL = 50000m,
            DailyReturn = 5.0,
            PositionsCount = 3
        };

        // Act
        await _repository.SaveAsync("TestAccount", snapshot);

        // Assert
        var saved = await _repository.GetSnapshotByDateAsync("TestAccount", snapshot.Date);
        Assert.IsNotNull(saved);
        Assert.AreEqual(snapshot.Currency, saved.Currency);
        Assert.AreEqual(snapshot.TotalEquity, saved.TotalEquity);
    }

    [TestMethod]
    public async Task GetLatestSnapshotAsync_ReturnsLatestSnapshot()
    {
        // Arrange
        var snapshot1 = new DailySnapshot
        {
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
            Currency = CurrencyType.KRW,
            TotalEquity = 1000000m,
            CashBalance = 500000m,
            PositionValue = 500000m,
            DailyPnL = 0m,
            DailyReturn = 0.0,
            PositionsCount = 2
        };
        var snapshot2 = new DailySnapshot
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Currency = CurrencyType.KRW,
            TotalEquity = 1100000m,
            CashBalance = 550000m,
            PositionValue = 550000m,
            DailyPnL = 100000m,
            DailyReturn = 10.0,
            PositionsCount = 3
        };

        await _repository.SaveAsync("TestAccount", snapshot1);
        await _repository.SaveAsync("TestAccount", snapshot2);

        // Act
        var latest = await _repository.GetLatestSnapshotAsync("TestAccount");

        // Assert
        Assert.IsNotNull(latest);
        Assert.AreEqual(snapshot2.Date, latest.Date);
        Assert.AreEqual(snapshot2.TotalEquity, latest.TotalEquity);
    }
}
