using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace yQuant.Infra.Persistence.Tests;

[TestClass]
public class FirebirdScheduledOrderRepositoryTests
{
    private IConfiguration _config;
    private Mock<ILogger<FirebirdScheduledOrderRepository>> _loggerMock;
    private string _connectionString = "User=SYSDBA;Password=masterkey;Database=test.fdb;DataSource=localhost;Port=3050";

    [TestInitialize]
    public void Setup()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "ConnectionStrings:Firebird", _connectionString }
            })
            .Build();
        _loggerMock = new Mock<ILogger<FirebirdScheduledOrderRepository>>();
    }

    [TestMethod]
    public void Constructor_WithMissingConnectionString_ThrowsException()
    {
        // Arrange
        var emptyConfig = new Mock<IConfiguration>();

        // Act & Assert
        try
        {
            new FirebirdScheduledOrderRepository(emptyConfig.Object, _loggerMock.Object);
            Assert.Fail("Should have thrown an exception");
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException)
        {
            // Expected - GetConnectionString can throw either depending on config state
        }
    }

    [TestMethod]
    public void Constructor_WithConnectionString_InitializesCorrectly()
    {
        // Act
        var repo = new FirebirdScheduledOrderRepository(_config, _loggerMock.Object);

        // Assert
        Assert.IsNotNull(repo);
    }
}
