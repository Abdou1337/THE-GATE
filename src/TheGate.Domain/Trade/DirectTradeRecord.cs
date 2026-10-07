namespace TheGate.Domain.Trade;

public enum DirectTradeRecordStatus
{
    AwaitingProducerConfirmation,
    Confirmed,
    Closed
}

public sealed class DirectTradeRecord
{
    public DirectTradeRecord(
        Guid id,
        Guid offerId,
        Guid producerOrganizationId,
        Guid buyerOrganizationId,
        Guid initiatedByOrganizationId,
        Quantity agreedQuantity,
        DateTimeOffset recordedAtUtc,
        DirectTradeRecordStatus status = DirectTradeRecordStatus.AwaitingProducerConfirmation,
        DateTimeOffset? producerConfirmedAtUtc = null,
        DateTimeOffset? producerClosedAtUtc = null,
        DateTimeOffset? buyerClosedAtUtc = null)
    {
        if (id == Guid.Empty || offerId == Guid.Empty)
        {
            throw new ArgumentException("Trade and offer IDs cannot be empty.");
        }

        if (producerOrganizationId == Guid.Empty ||
            buyerOrganizationId == Guid.Empty ||
            producerOrganizationId == buyerOrganizationId)
        {
            throw new ArgumentException("Producer and buyer must be distinct, identified organizations.");
        }

        if (initiatedByOrganizationId != producerOrganizationId &&
            initiatedByOrganizationId != buyerOrganizationId)
        {
            throw new ArgumentException("A direct trade record must be initiated by the producer or buyer.", nameof(initiatedByOrganizationId));
        }

        ArgumentNullException.ThrowIfNull(agreedQuantity);
        if (recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Record time must be UTC.", nameof(recordedAtUtc));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if ((status is DirectTradeRecordStatus.Confirmed or DirectTradeRecordStatus.Closed) !=
                (producerConfirmedAtUtc is not null) ||
            (producerConfirmedAtUtc is not null && producerConfirmedAtUtc.Value.Offset != TimeSpan.Zero))
        {
            throw new ArgumentException("Producer confirmation time must be UTC and match the record status.", nameof(producerConfirmedAtUtc));
        }

        if ((producerClosedAtUtc is not null && producerClosedAtUtc.Value.Offset != TimeSpan.Zero) ||
            (buyerClosedAtUtc is not null && buyerClosedAtUtc.Value.Offset != TimeSpan.Zero) ||
            (status == DirectTradeRecordStatus.Closed &&
             (producerClosedAtUtc is null || buyerClosedAtUtc is null)) ||
            (status != DirectTradeRecordStatus.Closed &&
             (producerClosedAtUtc is not null || buyerClosedAtUtc is not null)))
        {
            throw new ArgumentException("Closed status requires both UTC closure confirmations.", nameof(status));
        }

        Id = id;
        OfferId = offerId;
        ProducerOrganizationId = producerOrganizationId;
        BuyerOrganizationId = buyerOrganizationId;
        InitiatedByOrganizationId = initiatedByOrganizationId;
        AgreedQuantity = agreedQuantity;
        RecordedAtUtc = recordedAtUtc;
        Status = status;
        ProducerConfirmedAtUtc = producerConfirmedAtUtc;
        ProducerClosedAtUtc = producerClosedAtUtc;
        BuyerClosedAtUtc = buyerClosedAtUtc;
    }

    public Guid Id { get; }

    public Guid OfferId { get; }

    public Guid ProducerOrganizationId { get; }

    public Guid BuyerOrganizationId { get; }

    public Guid InitiatedByOrganizationId { get; }

    public Quantity AgreedQuantity { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public DirectTradeRecordStatus Status { get; private set; }

    public DateTimeOffset? ProducerConfirmedAtUtc { get; private set; }

    public DateTimeOffset? ProducerClosedAtUtc { get; private set; }

    public DateTimeOffset? BuyerClosedAtUtc { get; private set; }

    public void ConfirmByProducer(Guid producerOrganizationId, DateTimeOffset confirmedAtUtc)
    {
        if (producerOrganizationId != ProducerOrganizationId)
        {
            throw new InvalidOperationException("Only the producer associated with this offer can confirm the trade record.");
        }

        if (Status != DirectTradeRecordStatus.AwaitingProducerConfirmation)
        {
            throw new InvalidOperationException("Trade record has already been confirmed.");
        }

        if (confirmedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Confirmation time must be UTC.", nameof(confirmedAtUtc));
        }

        Status = DirectTradeRecordStatus.Confirmed;
        ProducerConfirmedAtUtc = confirmedAtUtc;
    }

    public void CloseByParty(Guid organizationId, DateTimeOffset closedAtUtc)
    {
        if (Status != DirectTradeRecordStatus.Confirmed)
        {
            throw new InvalidOperationException("Only a confirmed trade can be closed.");
        }

        if (closedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Closure time must be UTC.", nameof(closedAtUtc));
        }

        if (organizationId == ProducerOrganizationId)
        {
            if (ProducerClosedAtUtc is not null)
            {
                throw new InvalidOperationException("Producer has already confirmed closure.");
            }

            ProducerClosedAtUtc = closedAtUtc;
        }
        else if (organizationId == BuyerOrganizationId)
        {
            if (BuyerClosedAtUtc is not null)
            {
                throw new InvalidOperationException("Buyer has already confirmed closure.");
            }

            BuyerClosedAtUtc = closedAtUtc;
        }
        else
        {
            throw new InvalidOperationException("Only the producer or buyer can confirm closure.");
        }

        if (ProducerClosedAtUtc is not null && BuyerClosedAtUtc is not null)
        {
            Status = DirectTradeRecordStatus.Closed;
        }
    }

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

        return new DirectTradeRecord(
            Guid.NewGuid(),
            offer.Id,
            offer.ProducerOrganizationId,
            buyerOrganizationId,
            initiatedByOrganizationId,
            agreedQuantity,
            recordedAtUtc);
    }
}
