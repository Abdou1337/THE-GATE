using TheGate.Domain.Trade;

namespace TheGate.Application.Trade;

public sealed class DirectTradeWorkflow(ITradeRepository repository)
{
    public async Task<ProductOffer> CreateOfferAsync(
        Guid producerOrganizationId,
        string productDescription,
        Quantity declaredQuantity,
        Quantity minimumDirectTradeQuantity,
        Uri? externalContactUri,
        CancellationToken cancellationToken)
    {
        var offer = new ProductOffer(
            Guid.NewGuid(),
            producerOrganizationId,
            productDescription,
            declaredQuantity,
            minimumDirectTradeQuantity,
            externalContactUri);

        await repository.AddOfferAsync(offer, cancellationToken);
        return offer;
    }

    public Task<IReadOnlyList<OfferListing>> GetOffersAsync(CancellationToken cancellationToken) =>
        repository.GetOffersAsync(cancellationToken);

    public Task<OfferListing?> GetOfferAsync(Guid offerId, CancellationToken cancellationToken) =>
        repository.GetOfferAsync(offerId, cancellationToken);

    public Task<TradeRegistrationResult> RegisterDirectTradeAsync(
        Guid offerId,
        Guid buyerOrganizationId,
        Quantity agreedQuantity,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken) =>
        repository.RegisterDirectTradeAsync(
            offerId,
            buyerOrganizationId,
            agreedQuantity,
            recordedAtUtc,
            cancellationToken);

    public Task<DirectTradeRecord?> GetTradeRecordAsync(Guid tradeRecordId, CancellationToken cancellationToken) =>
        repository.GetTradeRecordAsync(tradeRecordId, cancellationToken);

    public Task<IndependentVerificationReport> AddVerificationAsync(
        Guid tradeRecordId,
        Guid verifierOrganizationId,
        Quantity measuredQuantity,
        string evidenceReference,
        DateTimeOffset inspectedAtUtc,
        CancellationToken cancellationToken) =>
        repository.AddVerificationAsync(
            tradeRecordId,
            verifierOrganizationId,
            measuredQuantity,
            evidenceReference,
            inspectedAtUtc,
            cancellationToken);

    public Task<IReadOnlyList<IndependentVerificationReport>> GetVerificationsAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken) =>
        repository.GetVerificationsAsync(tradeRecordId, cancellationToken);
}
