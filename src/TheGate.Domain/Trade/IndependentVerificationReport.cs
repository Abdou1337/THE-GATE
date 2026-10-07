namespace TheGate.Domain.Trade;

public sealed record IndependentVerificationReport
{
    public IndependentVerificationReport(
        Guid id,
        DirectTradeRecord tradeRecord,
        Guid verifierOrganizationId,
        Quantity measuredQuantity,
        string evidenceReference,
        DateTimeOffset inspectedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Verification report ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(tradeRecord);
        ArgumentNullException.ThrowIfNull(measuredQuantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        if (evidenceReference.Trim().Length > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceReference), "Evidence reference cannot exceed 2048 characters.");
        }

        if (verifierOrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Verifier organization ID cannot be empty.", nameof(verifierOrganizationId));
        }

        if (verifierOrganizationId == tradeRecord.ProducerOrganizationId ||
            verifierOrganizationId == tradeRecord.BuyerOrganizationId)
        {
            throw new ArgumentException("Verifier must be independent of the producer and buyer.", nameof(verifierOrganizationId));
        }

        if (!tradeRecord.AgreedQuantity.HasSameUnit(measuredQuantity))
        {
            throw new ArgumentException("Measured quantity must use the trade record's unit.", nameof(measuredQuantity));
        }

        if (inspectedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Inspection time must be UTC.", nameof(inspectedAtUtc));
        }

        Id = id;
        TradeRecordId = tradeRecord.Id;
        VerifierOrganizationId = verifierOrganizationId;
        MeasuredQuantity = measuredQuantity;
        EvidenceReference = evidenceReference.Trim();
        InspectedAtUtc = inspectedAtUtc;
    }

    public Guid Id { get; }

    public Guid TradeRecordId { get; }

    public Guid VerifierOrganizationId { get; }

    public Quantity MeasuredQuantity { get; }

    public string EvidenceReference { get; }

    public DateTimeOffset InspectedAtUtc { get; }
}
