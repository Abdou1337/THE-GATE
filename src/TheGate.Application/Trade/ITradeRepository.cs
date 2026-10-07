using TheGate.Domain.Trade;

namespace TheGate.Application.Trade;

public interface ITradeRepository
{
    Task AddOfferAsync(ProductOffer offer, CancellationToken cancellationToken);

    Task<IReadOnlyList<OfferListing>> GetOffersAsync(CancellationToken cancellationToken);

    Task<OfferListing?> GetOfferAsync(Guid offerId, CancellationToken cancellationToken);

    Task<TradeRegistrationResult> RegisterDirectTradeAsync(
        Guid offerId,
        Guid buyerOrganizationId,
        Quantity agreedQuantity,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken);

    Task<TradeConfirmationResult> ConfirmTradeAsync(
        Guid tradeRecordId,
        Guid producerOrganizationId,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken);

    Task<DirectTradeRecord?> GetTradeRecordAsync(Guid tradeRecordId, CancellationToken cancellationToken);

    Task<IndependentVerificationReport> AddVerificationAsync(
        Guid tradeRecordId,
        Guid verifierOrganizationId,
        Quantity measuredQuantity,
        string evidenceReference,
        DateTimeOffset inspectedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IndependentVerificationReport>> GetVerificationsAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken);
}

public sealed record OfferListing(
    Guid Id,
    Guid ProducerOrganizationId,
    string ProductDescription,
    decimal DeclaredQuantity,
    decimal MinimumDirectTradeQuantity,
    decimal RemainingQuantity,
    string UnitCode,
    Uri? ExternalContactUri);

public enum TradeRegistrationStatus
{
    Registered,
    OfferNotFound,
    InsufficientQuantity
}

public sealed record TradeRegistrationResult(TradeRegistrationStatus Status, DirectTradeRecord? TradeRecord);

public enum TradeConfirmationStatus
{
    Confirmed,
    TradeRecordNotFound,
    NotProducer,
    AlreadyConfirmed,
    InsufficientQuantity
}

public sealed record TradeConfirmationResult(
    TradeConfirmationStatus Status,
    DirectTradeRecord? TradeRecord);
