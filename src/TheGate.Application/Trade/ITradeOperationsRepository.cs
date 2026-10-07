using TheGate.Domain.Trade;

namespace TheGate.Application.Trade;

public interface ITradeOperationsRepository
{
    Task<IReadOnlyList<ComplianceTaskView>> GetComplianceTasksAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken);

    Task<ComplianceTaskView?> CreateComplianceTaskAsync(
        Guid tradeRecordId,
        Guid responsibleOrganizationId,
        string documentType,
        string issuer,
        string requirementSource,
        DateTimeOffset? dueAtUtc,
        CancellationToken cancellationToken);

    Task<ComplianceTaskView?> RecordComplianceEvidenceAsync(
        Guid taskId,
        Guid actingOrganizationId,
        string evidenceReference,
        CancellationToken cancellationToken);

    Task<ComplianceTaskView?> AttestComplianceTaskAsync(
        Guid taskId,
        Guid actingOrganizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentObligation>> GetPaymentObligationsAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken);

    Task<PaymentObligation?> CreatePaymentObligationAsync(
        Guid tradeRecordId,
        Guid payerOrganizationId,
        Guid beneficiaryOrganizationId,
        Guid paymentPartnerOrganizationId,
        decimal amount,
        string currencyCode,
        string providerName,
        string providerReference,
        CancellationToken cancellationToken);

    Task<PaymentObligation?> RecordPartnerSettlementAsync(
        Guid obligationId,
        Guid paymentPartnerOrganizationId,
        DateTimeOffset reportedAtUtc,
        CancellationToken cancellationToken);

    Task<LogisticsShipmentView?> CreateShipmentAsync(
        Guid tradeRecordId,
        Guid providerOrganizationId,
        string shipmentReference,
        CancellationToken cancellationToken);

    Task<LogisticsMilestoneView?> RecordLogisticsMilestoneAsync(
        Guid tradeRecordId,
        Guid shipmentId,
        Guid providerOrganizationId,
        LogisticsMilestoneType milestone,
        string evidenceReference,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LogisticsMilestoneView>> GetLogisticsMilestonesAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken);

    Task<TradeClosureResult> ConfirmTradeClosureAsync(
        Guid tradeRecordId,
        Guid organizationId,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken);
}

public sealed record ComplianceTaskView(
    Guid Id,
    Guid TradeRecordId,
    Guid ResponsibleOrganizationId,
    string DocumentType,
    string Issuer,
    string RequirementSource,
    DateTimeOffset? DueAtUtc,
    string Status,
    string? EvidenceReference);

public sealed record LogisticsShipmentView(
    Guid Id,
    Guid TradeRecordId,
    Guid ResponsibleProviderOrganizationId,
    string ShipmentReference);

public sealed record LogisticsMilestoneView(
    Guid Id,
    Guid ShipmentId,
    Guid ReportedByOrganizationId,
    LogisticsMilestoneType Type,
    string EvidenceReference,
    DateTimeOffset RecordedAtUtc);

public enum TradeClosureStatus
{
    Closed,
    TradeRecordNotFound,
    NotTradeParty,
    NotConfirmed,
    ComplianceIncomplete,
    PaymentOutstanding,
    LogisticsIncomplete,
    AlreadyConfirmed,
    ConfirmationRecorded
}

public sealed record TradeClosureResult(TradeClosureStatus Status, DirectTradeRecord? TradeRecord);
