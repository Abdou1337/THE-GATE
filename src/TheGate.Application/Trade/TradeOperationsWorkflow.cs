using TheGate.Domain.Trade;

namespace TheGate.Application.Trade;

public sealed class TradeOperationsWorkflow(ITradeOperationsRepository repository)
{
    public Task<IReadOnlyList<ComplianceTaskView>> GetComplianceTasksAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken) =>
        repository.GetComplianceTasksAsync(tradeRecordId, cancellationToken);

    public Task<ComplianceTaskView?> CreateComplianceTaskAsync(
        Guid tradeRecordId,
        Guid responsibleOrganizationId,
        string documentType,
        string issuer,
        string requirementSource,
        DateTimeOffset? dueAtUtc,
        CancellationToken cancellationToken) =>
        repository.CreateComplianceTaskAsync(
            tradeRecordId,
            responsibleOrganizationId,
            documentType,
            issuer,
            requirementSource,
            dueAtUtc,
            cancellationToken);

    public Task<ComplianceTaskView?> RecordComplianceEvidenceAsync(
        Guid taskId,
        Guid actingOrganizationId,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        repository.RecordComplianceEvidenceAsync(taskId, actingOrganizationId, evidenceReference, cancellationToken);

    public Task<ComplianceTaskView?> AttestComplianceTaskAsync(
        Guid taskId,
        Guid actingOrganizationId,
        CancellationToken cancellationToken) =>
        repository.AttestComplianceTaskAsync(taskId, actingOrganizationId, cancellationToken);

    public Task<IReadOnlyList<PaymentObligation>> GetPaymentObligationsAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken) =>
        repository.GetPaymentObligationsAsync(tradeRecordId, cancellationToken);

    public Task<PaymentObligation?> CreatePaymentObligationAsync(
        Guid tradeRecordId,
        Guid payerOrganizationId,
        Guid beneficiaryOrganizationId,
        Guid paymentPartnerOrganizationId,
        decimal amount,
        string currencyCode,
        string providerName,
        string providerReference,
        CancellationToken cancellationToken) =>
        repository.CreatePaymentObligationAsync(
            tradeRecordId,
            payerOrganizationId,
            beneficiaryOrganizationId,
            paymentPartnerOrganizationId,
            amount,
            currencyCode,
            providerName,
            providerReference,
            cancellationToken);

    public Task<PaymentObligation?> RecordPartnerSettlementAsync(
        Guid obligationId,
        Guid paymentPartnerOrganizationId,
        DateTimeOffset reportedAtUtc,
        CancellationToken cancellationToken) =>
        repository.RecordPartnerSettlementAsync(
            obligationId,
            paymentPartnerOrganizationId,
            reportedAtUtc,
            cancellationToken);

    public Task<LogisticsShipmentView?> CreateShipmentAsync(
        Guid tradeRecordId,
        Guid providerOrganizationId,
        string shipmentReference,
        CancellationToken cancellationToken) =>
        repository.CreateShipmentAsync(
            tradeRecordId,
            providerOrganizationId,
            shipmentReference,
            cancellationToken);

    public Task<LogisticsMilestoneView?> RecordLogisticsMilestoneAsync(
        Guid tradeRecordId,
        Guid shipmentId,
        Guid providerOrganizationId,
        LogisticsMilestoneType milestone,
        string evidenceReference,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken) =>
        repository.RecordLogisticsMilestoneAsync(
            tradeRecordId,
            shipmentId,
            providerOrganizationId,
            milestone,
            evidenceReference,
            recordedAtUtc,
            cancellationToken);

    public Task<IReadOnlyList<LogisticsMilestoneView>> GetLogisticsMilestonesAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken) =>
        repository.GetLogisticsMilestonesAsync(tradeRecordId, cancellationToken);

    public Task<TradeClosureResult> ConfirmTradeClosureAsync(
        Guid tradeRecordId,
        Guid organizationId,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken) =>
        repository.ConfirmTradeClosureAsync(
            tradeRecordId,
            organizationId,
            confirmedAtUtc,
            cancellationToken);
}
