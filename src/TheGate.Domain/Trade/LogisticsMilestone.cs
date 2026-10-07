namespace TheGate.Domain.Trade;

public enum LogisticsMilestoneType
{
    Quotation = 1,
    Booking = 2,
    Collection = 3,
    Warehouse = 4,
    Containerization = 5,
    ExportClearance = 6,
    Shipment = 7,
    Tracking = 8,
    Arrival = 9,
    Delivery = 10
}

public sealed record LogisticsMilestone
{
    public LogisticsMilestone(
        Guid id,
        Guid shipmentId,
        Guid reportedByOrganizationId,
        LogisticsMilestoneType type,
        string evidenceReference,
        DateTimeOffset recordedAtUtc)
    {
        if (id == Guid.Empty || shipmentId == Guid.Empty || reportedByOrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Milestone, shipment, and reporting organization IDs are required.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        if (evidenceReference.Trim().Length > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceReference));
        }

        if (recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Milestone time must be UTC.", nameof(recordedAtUtc));
        }

        Id = id;
        ShipmentId = shipmentId;
        ReportedByOrganizationId = reportedByOrganizationId;
        Type = type;
        EvidenceReference = evidenceReference.Trim();
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; }

    public Guid ShipmentId { get; }

    public Guid ReportedByOrganizationId { get; }

    public LogisticsMilestoneType Type { get; }

    public string EvidenceReference { get; }

    public DateTimeOffset RecordedAtUtc { get; }
}
