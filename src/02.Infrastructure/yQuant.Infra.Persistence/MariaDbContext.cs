using Microsoft.EntityFrameworkCore;
using yQuant.Core.Models;

namespace yQuant.Infra.Persistence;

/// <summary>
/// Entity Framework Core DbContext for MariaDB persistence.
/// </summary>
public class MariaDbContext : DbContext
{
    public MariaDbContext(DbContextOptions<MariaDbContext> options) : base(options)
    {
    }

    public DbSet<TradeRecordEntity> Trades { get; set; } = null!;
    public DbSet<StockCatalogEntity> Catalog { get; set; } = null!;
    public DbSet<CatalogMetadataEntity> CatalogMetadata { get; set; } = null!;
    public DbSet<KisTokenEntity> Tokens { get; set; } = null!;
    public DbSet<ScheduledOrderEntity> ScheduledOrders { get; set; } = null!;
    public DbSet<DailySnapshotEntity> DailySnapshots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Trade Records
        modelBuilder.Entity<TradeRecordEntity>(entity =>
        {
            entity.ToTable("trades");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(38).IsRequired();
            entity.Property(e => e.ExecutedAt).IsRequired();
            entity.Property(e => e.Ticker).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.ExecutedPrice).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Commission).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.OrderId).HasMaxLength(50);
            entity.Property(e => e.BrokerOrderId).HasMaxLength(50);
            entity.Property(e => e.Strategy).HasMaxLength(50);
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Exchange).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AccountAlias).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new { e.AccountAlias, e.ExecutedAt }).HasDatabaseName("ix_trades_account_date");
        });

        // Stock Catalog
        modelBuilder.Entity<StockCatalogEntity>(entity =>
        {
            entity.ToTable("catalog");
            entity.HasKey(e => e.Ticker);
            entity.Property(e => e.Ticker).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Exchange).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(10).IsRequired();
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Country).HasDatabaseName("ix_catalog_country");
        });

        // Catalog Metadata
        modelBuilder.Entity<CatalogMetadataEntity>(entity =>
        {
            entity.ToTable("catalog_metadata");
            entity.HasKey(e => e.KeyName);
            entity.Property(e => e.KeyName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ValueText).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // KIS Tokens
        modelBuilder.Entity<KisTokenEntity>(entity =>
        {
            entity.ToTable("tokens");
            entity.HasKey(e => e.AccountAlias);
            entity.Property(e => e.AccountAlias).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Broker).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Token).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Expiration).IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Scheduled Orders
        modelBuilder.Entity<ScheduledOrderEntity>(entity =>
        {
            entity.ToTable("scheduled_orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(38).IsRequired();
            entity.Property(e => e.AccountAlias).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Data).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.LockedAt);
            entity.Property(e => e.LockedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.AccountAlias).HasDatabaseName("ix_scheduled_account");
        });

        // Daily Snapshots
        modelBuilder.Entity<DailySnapshotEntity>(entity =>
        {
            entity.ToTable("daily_snapshots");
            entity.HasKey(e => new { e.AccountAlias, e.SnapshotDate, e.Currency });
            entity.Property(e => e.AccountAlias).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SnapshotDate).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Data).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.AccountAlias, e.SnapshotDate }).HasDatabaseName("ix_snapshot_account_date");
        });
    }
}

// Entity Classes
public class TradeRecordEntity
{
    public string Id { get; set; } = null!;
    public DateTime ExecutedAt { get; set; }
    public string Ticker { get; set; } = null!;
    public string Action { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ExecutedPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
    public string? OrderId { get; set; }
    public string? BrokerOrderId { get; set; }
    public string? Strategy { get; set; }
    public string Currency { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public string AccountAlias { get; set; } = null!;

    public static TradeRecordEntity FromModel(string accountAlias, TradeRecord model) => new()
    {
        Id = model.Id.ToString().ToUpper(),
        ExecutedAt = model.ExecutedAt,
        Ticker = model.Ticker,
        Action = model.Action.ToString(),
        Quantity = model.Quantity,
        ExecutedPrice = model.ExecutedPrice,
        Amount = model.Amount,
        Commission = model.Commission,
        OrderId = model.OrderId,
        BrokerOrderId = model.BrokerOrderId,
        Strategy = model.Strategy,
        Currency = model.Currency.ToString(),
        Exchange = model.Exchange.ToString(),
        AccountAlias = accountAlias
    };

    public TradeRecord ToModel() => new()
    {
        Id = Guid.Parse(Id),
        ExecutedAt = ExecutedAt,
        Ticker = Ticker,
        Action = Enum.Parse<OrderAction>(Action),
        Quantity = Quantity,
        ExecutedPrice = ExecutedPrice,
        Commission = Commission,
        OrderId = OrderId,
        BrokerOrderId = BrokerOrderId,
        Strategy = Strategy,
        Currency = Enum.Parse<CurrencyType>(Currency),
        Exchange = Enum.Parse<ExchangeCode>(Exchange)
    };
}

public class StockCatalogEntity
{
    public string Ticker { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string Country { get; set; } = null!;
    public DateTime LastUpdated { get; set; }

    public Stock ToModel() => new()
    {
        Ticker = Ticker,
        Name = Name,
        Exchange = Exchange,
        Currency = Enum.Parse<CurrencyType>(Currency)
    };
}

public class CatalogMetadataEntity
{
    public string KeyName { get; set; } = null!;
    public string ValueText { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}

public class KisTokenEntity
{
    public string AccountAlias { get; set; } = null!;
    public string Broker { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime Expiration { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ScheduledOrderEntity
{
    public string Id { get; set; } = null!;
    public string AccountAlias { get; set; } = null!;
    public string Data { get; set; } = null!;
    public DateTime? LockedAt { get; set; }
    public string? LockedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DailySnapshotEntity
{
    public string AccountAlias { get; set; } = null!;
    public DateTime SnapshotDate { get; set; }
    public string Currency { get; set; } = null!;
    public string Data { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
