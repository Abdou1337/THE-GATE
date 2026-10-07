namespace TheGate.Domain.Trade;

public sealed class DirectTradeRecord
{
    private DirectTradeRecord(
        Guid id,
        Guid offerId,
        Guid producerOrganizationId,
        Guid buyerOrganizationId,
        Guid initiatedByOrganizationId,
        Quantity agreedQuantity,
        DateTimeOffset recordedAtUtc)
    {
        Id = id;
        OfferId = offerId;
        ProducerOrganizationId = producerOrganizationId;
        BuyerOrganizationId = buyerOrganizationId;
        InitiatedByOrganizationId = initiatedByOrganizationId;
        AgreedQuantity = agreedQuantity;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; }

    public Guid OfferId { get; }

    public Guid ProducerOrganizationId { get; }

    public Guid BuyerOrganizationId { get; }

    public Guid InitiatedByOrganizationId { get; }

    public Quantity AgreedQuantity { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public static DirectTradeRecord Register(
        ProductOffer offer,
        Guid buyerOrganizationId,
        Guid initiatedByOrganizationId,
        Quantity agreedQuantity,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(agreedQuantity);

        if (buyerOrganizationId == Guid.Empty || buyerOrganizationId == offer.ProducerOrganizationId)
        {
            throw new ArgumentException("Buyer must be a distinct, identified organization.", nameof(buyerOrganizationId));
        }

        if (initiatedByOrganizationId != offer.ProducerOrganizationId &&
            initiatedByOrganizationId != buyerOrganizationId)
        {
            throw new ArgumentException("A direct trade record must be initiated by the producer or buyer.", nameof(initiatedByOrganizationId));
        }

        if (recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Record time must be UTC.", nameof(recordedAtUtc));
        }

        if (!offer.DeclaredQuantity.HasSameUnit(agreedQuantity))
        {
            throw new ArgumentException("Agreed quantity must use the offer's unit.", nameof(agreedQuantity));
        }

        if (agreedQuantity.Value < offer.MinimumDirectTradeQuantity.Value)
        {
            throw new InvalidOperationException("Agreed quantity is below the minimum direct trade quantity.");
        }

        var id = Guid.NewGuid();
        offer.Allocate(id, agreedQuantity);

        return new DirectTradeRecord(
            id,
            offer.Id,
            offer.ProducerOrganizationId,
            buyerOrganizationId,
            initiatedByOrganizationId,
            agreedQuantity,
            recordedAtUtc);
    }
}
