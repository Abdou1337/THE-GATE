using System.Data;
using Microsoft.EntityFrameworkCore;
using TheGate.Application.Trade;
using TheGate.Domain.Trade;

namespace TheGate.Infrastructure.Persistence;

public sealed class EfTradeRepository(TradeDbContext dbContext) : ITradeRepository
{
    public async Task AddOfferAsync(ProductOffer offer, CancellationToken cancellationToken)
    {
        dbContext.ProductOffers.Add(ToRow(offer));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OfferListing>> GetOffersAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.ProductOffers
            .AsNoTracking()
            .OrderByDescending(row => row.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(ToListing).ToArray();
    }

    public async Task<OfferListing?> GetOfferAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var row = await dbContext.ProductOffers
            .AsNoTracking()
            .SingleOrDefaultAsync(offer => offer.Id == offerId, cancellationToken);

        return row is null ? null : ToListing(row);
    }

    public async Task<TradeRegistrationResult> RegisterDirectTradeAsync(
        Guid offerId,
        Guid buyerOrganizationId,
        Quantity agreedQuantity,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        var offerRow = await dbContext.ProductOffers
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == offerId, cancellationToken);

        if (offerRow is null)
        {
            return new TradeRegistrationResult(TradeRegistrationStatus.OfferNotFound, null);
        }

        var tradeRecord = DirectTradeRecord.Register(
            ToDomain(offerRow),
            buyerOrganizationId,
            buyerOrganizationId,
            agreedQuantity,
            recordedAtUtc);

        dbContext.DirectTradeRecords.Add(ToRow(tradeRecord));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TradeRegistrationResult(TradeRegistrationStatus.Registered, tradeRecord);
    }

    public async Task<TradeConfirmationResult> ConfirmTradeAsync(
        Guid tradeRecordId,
        Guid producerOrganizationId,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var tradeRow = await dbContext.DirectTradeRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == tradeRecordId, cancellationToken);

        if (tradeRow is null)
        {
            return new TradeConfirmationResult(TradeConfirmationStatus.TradeRecordNotFound, null);
        }

        if (tradeRow.ProducerOrganizationId != producerOrganizationId)
        {
            return new TradeConfirmationResult(TradeConfirmationStatus.NotProducer, null);
        }

        if (tradeRow.Status != nameof(DirectTradeRecordStatus.AwaitingProducerConfirmation))
        {
            return new TradeConfirmationResult(TradeConfirmationStatus.AlreadyConfirmed, null);
        }

        var tradeRecord = ToDomain(tradeRow);
        tradeRecord.ConfirmByProducer(producerOrganizationId, confirmedAtUtc);

        var tradeUpdated = await dbContext.DirectTradeRecords
            .Where(row =>
                row.Id == tradeRecordId &&
                row.Status == nameof(DirectTradeRecordStatus.AwaitingProducerConfirmation))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Status, nameof(DirectTradeRecordStatus.Confirmed))
                    .SetProperty(row => row.ProducerConfirmedAtUtc, confirmedAtUtc),
                cancellationToken);

        if (tradeUpdated == 0)
        {
            return new TradeConfirmationResult(TradeConfirmationStatus.AlreadyConfirmed, null);
        }

        var offerUpdated = await dbContext.ProductOffers
            .Where(row =>
                row.Id == tradeRow.OfferId &&
                row.UnitCode == tradeRow.UnitCode &&
                tradeRow.AgreedQuantity >= row.MinimumDirectTradeQuantity &&
                row.AllocatedQuantity + tradeRow.AgreedQuantity <= row.DeclaredQuantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    row => row.AllocatedQuantity,
                    row => row.AllocatedQuantity + tradeRow.AgreedQuantity),
                cancellationToken);

        if (offerUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new TradeConfirmationResult(TradeConfirmationStatus.InsufficientQuantity, null);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TradeConfirmationResult(TradeConfirmationStatus.Confirmed, tradeRecord);
    }

    public async Task<DirectTradeRecord?> GetTradeRecordAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.DirectTradeRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Id == tradeRecordId, cancellationToken);

        return row is null ? null : ToDomain(row);
    }

    public async Task<IndependentVerificationReport> AddVerificationAsync(
        Guid tradeRecordId,
        Guid verifierOrganizationId,
        Quantity measuredQuantity,
        string evidenceReference,
        DateTimeOffset inspectedAtUtc,
        CancellationToken cancellationToken)
    {
        var tradeRow = await dbContext.DirectTradeRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == tradeRecordId, cancellationToken)
            ?? throw new KeyNotFoundException("Trade record was not found.");
        var report = new IndependentVerificationReport(
            Guid.NewGuid(),
            ToDomain(tradeRow),
            verifierOrganizationId,
            measuredQuantity,
            evidenceReference,
            inspectedAtUtc);

        dbContext.VerificationReports.Add(ToRow(report));
        await dbContext.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<IReadOnlyList<IndependentVerificationReport>> GetVerificationsAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken)
    {
        var tradeRow = await dbContext.DirectTradeRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == tradeRecordId, cancellationToken);

        if (tradeRow is null)
        {
            return [];
        }

        var tradeRecord = ToDomain(tradeRow);
        var rows = await dbContext.VerificationReports
            .AsNoTracking()
            .Where(report => report.TradeRecordId == tradeRecordId)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => ToDomain(row, tradeRecord))
            .OrderBy(report => report.InspectedAtUtc)
            .ToArray();
    }

    private static ProductOfferRow ToRow(ProductOffer offer) =>
        new()
        {
            Id = offer.Id,
            ProducerOrganizationId = offer.ProducerOrganizationId,
            ProductDescription = offer.ProductDescription,
            DeclaredQuantity = offer.DeclaredQuantity.Value,
            MinimumDirectTradeQuantity = offer.MinimumDirectTradeQuantity.Value,
            UnitCode = offer.DeclaredQuantity.UnitCode,
            ExternalContactUri = offer.ExternalContactUri?.AbsoluteUri
        };

    private static OfferListing ToListing(ProductOfferRow row) =>
        new(
            row.Id,
            row.ProducerOrganizationId,
            row.ProductDescription,
            row.DeclaredQuantity,
            row.MinimumDirectTradeQuantity,
            row.DeclaredQuantity - row.AllocatedQuantity,
            row.UnitCode,
            row.ExternalContactUri is null ? null : new Uri(row.ExternalContactUri));

    private static ProductOffer ToDomain(ProductOfferRow row) =>
        new(
            row.Id,
            row.ProducerOrganizationId,
            row.ProductDescription,
            new Quantity(row.DeclaredQuantity, row.UnitCode),
            new Quantity(row.MinimumDirectTradeQuantity, row.UnitCode),
            row.ExternalContactUri is null ? null : new Uri(row.ExternalContactUri));

    private static DirectTradeRow ToRow(DirectTradeRecord tradeRecord) =>
        new()
        {
            Id = tradeRecord.Id,
            OfferId = tradeRecord.OfferId,
            ProducerOrganizationId = tradeRecord.ProducerOrganizationId,
            BuyerOrganizationId = tradeRecord.BuyerOrganizationId,
            InitiatedByOrganizationId = tradeRecord.InitiatedByOrganizationId,
            AgreedQuantity = tradeRecord.AgreedQuantity.Value,
            UnitCode = tradeRecord.AgreedQuantity.UnitCode,
            RecordedAtUtc = tradeRecord.RecordedAtUtc,
            Status = tradeRecord.Status.ToString(),
            ProducerConfirmedAtUtc = tradeRecord.ProducerConfirmedAtUtc
        };

    private static DirectTradeRecord ToDomain(DirectTradeRow row) =>
        new(
            row.Id,
            row.OfferId,
            row.ProducerOrganizationId,
            row.BuyerOrganizationId,
            row.InitiatedByOrganizationId,
            new Quantity(row.AgreedQuantity, row.UnitCode),
            row.RecordedAtUtc,
            Enum.Parse<DirectTradeRecordStatus>(row.Status),
            row.ProducerConfirmedAtUtc,
            row.ProducerClosedAtUtc,
            row.BuyerClosedAtUtc);

    private static VerificationRow ToRow(IndependentVerificationReport report) =>
        new()
        {
            Id = report.Id,
            TradeRecordId = report.TradeRecordId,
            VerifierOrganizationId = report.VerifierOrganizationId,
            MeasuredQuantity = report.MeasuredQuantity.Value,
            UnitCode = report.MeasuredQuantity.UnitCode,
            EvidenceReference = report.EvidenceReference,
            InspectedAtUtc = report.InspectedAtUtc
        };

    private static IndependentVerificationReport ToDomain(VerificationRow row, DirectTradeRecord tradeRecord) =>
        new(
            row.Id,
            tradeRecord,
            row.VerifierOrganizationId,
            new Quantity(row.MeasuredQuantity, row.UnitCode),
            row.EvidenceReference,
            row.InspectedAtUtc);
}
