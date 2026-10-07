using Microsoft.EntityFrameworkCore;

namespace TheGate.Infrastructure.Persistence;

public sealed class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<ProductOfferRow> ProductOffers => Set<ProductOfferRow>();

    public DbSet<DirectTradeRow> DirectTradeRecords => Set<DirectTradeRow>();

    public DbSet<VerificationRow> VerificationReports => Set<VerificationRow>();

    public DbSet<ComplianceTaskRow> ComplianceTasks => Set<ComplianceTaskRow>();

    public DbSet<PaymentObligationRow> PaymentObligations => Set<PaymentObligationRow>();

    public DbSet<LogisticsShipmentRow> LogisticsShipments => Set<LogisticsShipmentRow>();

    public DbSet<LogisticsMilestoneRow> LogisticsMilestones => Set<LogisticsMilestoneRow>();

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
                "\"Status\" IN ('AwaitingProducerConfirmation', 'Confirmed', 'Closed') AND ((\"Status\" = 'AwaitingProducerConfirmation' AND \"ProducerConfirmedAtUtc\" IS NULL) OR (\"Status\" IN ('Confirmed', 'Closed') AND \"ProducerConfirmedAtUtc\" IS NOT NULL)) AND ((\"Status\" = 'Closed' AND \"ProducerClosedAtUtc\" IS NOT NULL AND \"BuyerClosedAtUtc\" IS NOT NULL) OR (\"Status\" <> 'Closed' AND NOT (\"ProducerClosedAtUtc\" IS NOT NULL AND \"BuyerClosedAtUtc\" IS NOT NULL)))"));
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

        modelBuilder.Entity<ComplianceTaskRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.DocumentType).HasMaxLength(120).IsRequired();
            entity.Property(row => row.Issuer).HasMaxLength(200).IsRequired();
            entity.Property(row => row.RequirementSource).HasMaxLength(500).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(40).IsRequired();
            entity.Property(row => row.EvidenceReference).HasMaxLength(2048);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ComplianceTasks_Status",
                "\"Status\" IN ('Required', 'EvidenceRecorded', 'ResponsiblePartyAttested') AND ((\"Status\" = 'Required' AND \"EvidenceReference\" IS NULL) OR (\"Status\" IN ('EvidenceRecorded', 'ResponsiblePartyAttested') AND \"EvidenceReference\" IS NOT NULL))"));
            entity.HasOne<DirectTradeRow>()
                .WithMany()
                .HasForeignKey(row => row.TradeRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => row.TradeRecordId);
        });

        modelBuilder.Entity<PaymentObligationRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Amount).HasPrecision(18, 2);
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.ProviderName).HasMaxLength(120).IsRequired();
            entity.Property(row => row.ProviderReference).HasMaxLength(200).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(40).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PaymentObligations_Status",
                "\"Amount\" > 0 AND \"Status\" IN ('Pending', 'PartnerReportedSettled') AND ((\"Status\" = 'Pending' AND \"PartnerReportedSettledAtUtc\" IS NULL) OR (\"Status\" = 'PartnerReportedSettled' AND \"PartnerReportedSettledAtUtc\" IS NOT NULL))"));
            entity.HasOne<DirectTradeRow>()
                .WithMany()
                .HasForeignKey(row => row.TradeRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => row.TradeRecordId);
        });

        modelBuilder.Entity<LogisticsShipmentRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ShipmentReference).HasMaxLength(200).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_LogisticsShipments_LastMilestone",
                "\"LastMilestone\" >= 0 AND \"LastMilestone\" <= 10"));
            entity.HasOne<DirectTradeRow>()
                .WithMany()
                .HasForeignKey(row => row.TradeRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => new { row.TradeRecordId, row.ShipmentReference }).IsUnique();
        });

        modelBuilder.Entity<LogisticsMilestoneRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Type).HasMaxLength(40).IsRequired();
            entity.Property(row => row.EvidenceReference).HasMaxLength(2048).IsRequired();
            entity.HasOne<LogisticsShipmentRow>()
                .WithMany()
                .HasForeignKey(row => row.ShipmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => new { row.ShipmentId, row.Type }).IsUnique();
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

    public DateTimeOffset? ProducerClosedAtUtc { get; set; }

    public DateTimeOffset? BuyerClosedAtUtc { get; set; }
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

public sealed class ComplianceTaskRow
{
    public Guid Id { get; set; }

    public Guid TradeRecordId { get; set; }

    public Guid ResponsibleOrganizationId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string RequirementSource { get; set; } = string.Empty;

    public DateTimeOffset? DueAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? EvidenceReference { get; set; }
}

public sealed class PaymentObligationRow
{
    public Guid Id { get; set; }

    public Guid TradeRecordId { get; set; }

    public Guid PayerOrganizationId { get; set; }

    public Guid BeneficiaryOrganizationId { get; set; }

    public Guid PaymentPartnerOrganizationId { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderReference { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? PartnerReportedSettledAtUtc { get; set; }
}

public sealed class LogisticsShipmentRow
{
    public Guid Id { get; set; }

    public Guid TradeRecordId { get; set; }

    public Guid ResponsibleProviderOrganizationId { get; set; }

    public string ShipmentReference { get; set; } = string.Empty;

    public int LastMilestone { get; set; }
}

public sealed class LogisticsMilestoneRow
{
    public Guid Id { get; set; }

    public Guid ShipmentId { get; set; }

    public Guid ReportedByOrganizationId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string EvidenceReference { get; set; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; set; }
}
