namespace TheGate.Domain.Trade;

public enum ComplianceTaskStatus
{
    Required,
    EvidenceRecorded,
    ResponsiblePartyAttested
}

public sealed class ComplianceTask
{
    public ComplianceTask(
        Guid id,
        Guid tradeRecordId,
        Guid responsibleOrganizationId,
        string documentType,
        string issuer,
        string requirementSource,
        DateTimeOffset? dueAtUtc)
    {
        if (id == Guid.Empty || tradeRecordId == Guid.Empty || responsibleOrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Task, trade, and responsible organization IDs are required.");
        }

        ValidateText(documentType, nameof(documentType), 120);
        ValidateText(issuer, nameof(issuer), 200);
        ValidateText(requirementSource, nameof(requirementSource), 500);
        if (dueAtUtc is not null && dueAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Due date must be UTC.", nameof(dueAtUtc));
        }

        Id = id;
        TradeRecordId = tradeRecordId;
        ResponsibleOrganizationId = responsibleOrganizationId;
        DocumentType = documentType.Trim();
        Issuer = issuer.Trim();
        RequirementSource = requirementSource.Trim();
        DueAtUtc = dueAtUtc;
    }

    public Guid Id { get; }

    public Guid TradeRecordId { get; }

    public Guid ResponsibleOrganizationId { get; }

    public string DocumentType { get; }

    public string Issuer { get; }

    public string RequirementSource { get; }

    public DateTimeOffset? DueAtUtc { get; }

    public ComplianceTaskStatus Status { get; private set; }

    public string? EvidenceReference { get; private set; }

    public void RecordEvidence(Guid actingOrganizationId, string evidenceReference)
    {
        if (actingOrganizationId != ResponsibleOrganizationId)
        {
            throw new InvalidOperationException("Only the responsible organization can submit evidence.");
        }

        if (Status != ComplianceTaskStatus.Required)
        {
            throw new InvalidOperationException("Evidence can only be submitted once for a required task.");
        }

        ValidateText(evidenceReference, nameof(evidenceReference), 2048);
        Status = ComplianceTaskStatus.EvidenceRecorded;
        EvidenceReference = evidenceReference.Trim();
    }

    public void AttestByResponsibleParty(Guid actingOrganizationId)
    {
        if (actingOrganizationId != ResponsibleOrganizationId)
        {
            throw new InvalidOperationException("Only the responsible organization can attest this task.");
        }

        if (Status != ComplianceTaskStatus.EvidenceRecorded)
        {
            throw new InvalidOperationException("Evidence must be recorded before the responsible party can attest.");
        }

        Status = ComplianceTaskStatus.ResponsiblePartyAttested;
    }

    private static void ValidateText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }
    }
}
