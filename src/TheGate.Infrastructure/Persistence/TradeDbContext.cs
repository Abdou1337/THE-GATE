using Microsoft.EntityFrameworkCore;

namespace TheGate.Infrastructure.Persistence;

public sealed class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<ProductOfferRow> ProductOffers => Set<ProductOfferRow>();

    public DbSet<DirectTradeRow> DirectTradeRecords => Set<DirectTradeRow>();

    public DbSet<VerificationRow> VerificationReports => Set<VerificationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductOfferRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ProductDescription).HasMaxLength(300).IsRequired();
            entity.Property(row => row.UnitCode).HasMaxLength(16).IsRequired();
            entity.Property(row => row.DeclaredQuantity).HasPrecision(18, 3);
            entity.Property(row => row.MinimumDirectTradeQuantity).HasPrecision(18, 3);
            entity.Property(row => row.AllocatedQuantity).HasPrecision(18, 3);
            entity.Property(row => row.ExternalContactUri).HasMaxLength(2048);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ProductOffers_Quantity",
                "CAST(\"DeclaredQuantity\" AS NUMERIC) > 0 AND CAST(\"MinimumDirectTradeQuantity\" AS NUMERIC) > 0 AND CAST(\"MinimumDirectTradeQuantity\" AS NUMERIC) <= CAST(\"DeclaredQuantity\" AS NUMERIC) AND CAST(\"AllocatedQuantity\" AS NUMERIC) >= 0 AND CAST(\"AllocatedQuantity\" AS NUMERIC) <= CAST(\"DeclaredQuantity\" AS NUMERIC)"));
            entity.HasIndex(row => row.ProducerOrganizationId);
        });

        modelBuilder.Entity<DirectTradeRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.UnitCode).HasMaxLength(16).IsRequired();
            entity.Property(row => row.AgreedQuantity).HasPrecision(18, 3);
            entity.Property(row => row.Status).HasMaxLength(40).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_DirectTradeRecords_Status",
                "\"Status\" IN ('AwaitingProducerConfirmation', 'Confirmed')"));
            entity.HasOne<ProductOfferRow>()
                .WithMany()
                .HasForeignKey(row => row.OfferId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => row.ProducerOrganizationId);
            entity.HasIndex(row => row.BuyerOrganizationId);
        });

        modelBuilder.Entity<VerificationRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.UnitCode).HasMaxLength(16).IsRequired();
            entity.Property(row => row.MeasuredQuantity).HasPrecision(18, 3);
            entity.Property(row => row.EvidenceReference).HasMaxLength(2048).IsRequired();
            entity.HasOne<DirectTradeRow>()
                .WithMany()
                .HasForeignKey(row => row.TradeRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class ProductOfferRow
{
    public Guid Id { get; set; }

    public Guid ProducerOrganizationId { get; set; }

    public string ProductDescription { get; set; } = string.Empty;

    public decimal DeclaredQuantity { get; set; }

    public decimal MinimumDirectTradeQuantity { get; set; }

    public decimal AllocatedQuantity { get; set; }

    public string UnitCode { get; set; } = string.Empty;

    public string? ExternalContactUri { get; set; }
}

public sealed class DirectTradeRow
{
    public Guid Id { get; set; }

    public Guid OfferId { get; set; }

    public Guid ProducerOrganizationId { get; set; }

    public Guid BuyerOrganizationId { get; set; }

    public Guid InitiatedByOrganizationId { get; set; }

    public decimal AgreedQuantity { get; set; }

    public string UnitCode { get; set; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? ProducerConfirmedAtUtc { get; set; }
}

public sealed class VerificationRow
{
    public Guid Id { get; set; }

    public Guid TradeRecordId { get; set; }

    public Guid VerifierOrganizationId { get; set; }

    public decimal MeasuredQuantity { get; set; }

    public string UnitCode { get; set; } = string.Empty;

    public string EvidenceReference { get; set; } = string.Empty;

    public DateTimeOffset InspectedAtUtc { get; set; }
}
