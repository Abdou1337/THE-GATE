using System.Data;
using Microsoft.EntityFrameworkCore;
using TheGate.Application.Trade;
using TheGate.Domain.Trade;

namespace TheGate.Infrastructure.Persistence;

public sealed class EfTradeOperationsRepository(TradeDbContext dbContext) : ITradeOperationsRepository
{
    public async Task<IReadOnlyList<ComplianceTaskView>> GetComplianceTasksAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken) =>
        await dbContext.ComplianceTasks
            .AsNoTracking()
            .Where(row => row.TradeRecordId == tradeRecordId)
            .OrderBy(row => row.DocumentType)
            .Select(row => new ComplianceTaskView(
                row.Id,
                row.TradeRecordId,
                row.ResponsibleOrganizationId,
                row.DocumentType,
                row.Issuer,
                row.RequirementSource,
                row.DueAtUtc,
                row.Status,
                row.EvidenceReference))
            .ToListAsync(cancellationToken);

    public async Task<ComplianceTaskView?> CreateComplianceTaskAsync(
        Guid tradeRecordId,
        Guid responsibleOrganizationId,
        string documentType,
        string issuer,
        string requirementSource,
        DateTimeOffset? dueAtUtc,
        CancellationToken cancellationToken)
    {
        var trade = await GetConfirmedTradeAsync(tradeRecordId, cancellationToken);
        if (trade is null || !IsTradeParty(trade, responsibleOrganizationId))
        {
            return null;
        }

        var task = new ComplianceTask(
            Guid.NewGuid(),
            tradeRecordId,
            responsibleOrganizationId,
            documentType,
            issuer,
            requirementSource,
            dueAtUtc);
        var row = ToRow(task);
        dbContext.ComplianceTasks.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(row);
    }

    public async Task<ComplianceTaskView?> RecordComplianceEvidenceAsync(
        Guid taskId,
        Guid actingOrganizationId,
        string evidenceReference,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.ComplianceTasks.SingleOrDefaultAsync(
            task => task.Id == taskId,
            cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (row.ResponsibleOrganizationId != actingOrganizationId)
        {
            return null;
        }

        var trade = await GetConfirmedTradeAsync(row.TradeRecordId, cancellationToken);
        if (trade is null)
        {
            throw new InvalidOperationException("Compliance tasks can only be changed for an open confirmed trade.");
        }

        var task = ToDomain(row);
        task.RecordEvidence(actingOrganizationId, evidenceReference);
        row.Status = task.Status.ToString();
        row.EvidenceReference = task.EvidenceReference;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(row);
    }

    public async Task<ComplianceTaskView?> AttestComplianceTaskAsync(
        Guid taskId,
        Guid actingOrganizationId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.ComplianceTasks.SingleOrDefaultAsync(
            task => task.Id == taskId,
            cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (row.ResponsibleOrganizationId != actingOrganizationId)
        {
            return null;
        }

        var trade = await GetConfirmedTradeAsync(row.TradeRecordId, cancellationToken);
        if (trade is null)
        {
            throw new InvalidOperationException("Compliance tasks can only be changed for an open confirmed trade.");
        }

        var task = ToDomain(row);
        task.AttestByResponsibleParty(actingOrganizationId);
        row.Status = task.Status.ToString();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(row);
    }

    public async Task<IReadOnlyList<PaymentObligation>> GetPaymentObligationsAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.PaymentObligations
            .AsNoTracking()
            .Where(row => row.TradeRecordId == tradeRecordId)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<PaymentObligation?> CreatePaymentObligationAsync(
        Guid tradeRecordId,
        Guid payerOrganizationId,
        Guid beneficiaryOrganizationId,
        Guid paymentPartnerOrganizationId,
        decimal amount,
        string currencyCode,
        string providerName,
        string providerReference,
        CancellationToken cancellationToken)
    {
        var trade = await GetConfirmedTradeAsync(tradeRecordId, cancellationToken);
        if (trade is null ||
            !IsTradeParty(trade, payerOrganizationId) ||
            !IsTradeParty(trade, beneficiaryOrganizationId))
        {
            return null;
        }

        var obligation = new PaymentObligation(
            Guid.NewGuid(),
            tradeRecordId,
            payerOrganizationId,
            beneficiaryOrganizationId,
            paymentPartnerOrganizationId,
            amount,
            currencyCode,
            providerName,
            providerReference);
        var row = ToRow(obligation);
        dbContext.PaymentObligations.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(row);
    }

    public async Task<PaymentObligation?> RecordPartnerSettlementAsync(
        Guid obligationId,
        Guid paymentPartnerOrganizationId,
        DateTimeOffset reportedAtUtc,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.PaymentObligations.SingleOrDefaultAsync(
            obligation => obligation.Id == obligationId,
            cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (row.PaymentPartnerOrganizationId != paymentPartnerOrganizationId)
        {
            return null;
        }

        var trade = await GetConfirmedTradeAsync(row.TradeRecordId, cancellationToken);
        if (trade is null)
        {
            throw new InvalidOperationException("Payment obligations can only be updated for an open confirmed trade.");
        }

        var obligation = ToDomain(row);
        obligation.RecordProviderSettlement(reportedAtUtc);
        row.Status = obligation.Status.ToString();
        row.PartnerReportedSettledAtUtc = obligation.PartnerReportedSettledAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(row);
    }

    public async Task<LogisticsShipmentView?> CreateShipmentAsync(
        Guid tradeRecordId,
        Guid providerOrganizationId,
        string shipmentReference,
        CancellationToken cancellationToken)
    {
        var trade = await GetConfirmedTradeAsync(tradeRecordId, cancellationToken);
        if (trade is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(shipmentReference);
        if (shipmentReference.Trim().Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(shipmentReference));
        }

        var row = new LogisticsShipmentRow
        {
            Id = Guid.NewGuid(),
            TradeRecordId = tradeRecordId,
            ResponsibleProviderOrganizationId = providerOrganizationId,
            ShipmentReference = shipmentReference.Trim()
        };
        dbContext.LogisticsShipments.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(row);
    }

    public async Task<LogisticsMilestoneView?> RecordLogisticsMilestoneAsync(
        Guid tradeRecordId,
        Guid shipmentId,
        Guid providerOrganizationId,
        LogisticsMilestoneType milestone,
        string evidenceReference,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var trade = await GetConfirmedTradeAsync(tradeRecordId, cancellationToken);
        if (trade is null)
        {
            return null;
        }

        var shipment = await dbContext.LogisticsShipments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == shipmentId && row.TradeRecordId == tradeRecordId,
                cancellationToken);
        if (shipment is null)
        {
            return null;
        }

        if (shipment.ResponsibleProviderOrganizationId != providerOrganizationId)
        {
            return null;
        }

        var updated = await dbContext.LogisticsShipments
            .Where(row =>
                row.Id == shipmentId &&
                row.LastMilestone == (int)milestone - 1)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.LastMilestone, (int)milestone),
                cancellationToken);
        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var milestoneRecord = new LogisticsMilestone(
            Guid.NewGuid(),
            shipmentId,
            providerOrganizationId,
            milestone,
            evidenceReference,
            recordedAtUtc);
        var row = ToRow(milestoneRecord);
        dbContext.LogisticsMilestones.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(row);
    }

    public async Task<IReadOnlyList<LogisticsMilestoneView>> GetLogisticsMilestonesAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.LogisticsMilestones
            .AsNoTracking()
            .Where(row => dbContext.LogisticsShipments.Any(
                shipment => shipment.Id == row.ShipmentId && shipment.TradeRecordId == tradeRecordId))
            .ToListAsync(cancellationToken);
        return rows
            .Select(ToView)
            .OrderBy(row => row.RecordedAtUtc)
            .ToArray();
    }

    public async Task<TradeClosureResult> ConfirmTradeClosureAsync(
        Guid tradeRecordId,
        Guid organizationId,
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
            return new TradeClosureResult(TradeClosureStatus.TradeRecordNotFound, null);
        }

        if (organizationId != tradeRow.ProducerOrganizationId &&
            organizationId != tradeRow.BuyerOrganizationId)
        {
            return new TradeClosureResult(TradeClosureStatus.NotTradeParty, null);
        }

        if (tradeRow.Status == nameof(DirectTradeRecordStatus.Closed))
        {
            return new TradeClosureResult(TradeClosureStatus.AlreadyConfirmed, ToDomain(tradeRow));
        }

        if (tradeRow.Status != nameof(DirectTradeRecordStatus.Confirmed))
        {
            return new TradeClosureResult(TradeClosureStatus.NotConfirmed, null);
        }

        if (await dbContext.ComplianceTasks.AnyAsync(
                row => row.TradeRecordId == tradeRecordId &&
                       row.Status != nameof(ComplianceTaskStatus.ResponsiblePartyAttested),
                cancellationToken))
        {
            return new TradeClosureResult(TradeClosureStatus.ComplianceIncomplete, null);
        }

        if (await dbContext.PaymentObligations.AnyAsync(
                row => row.TradeRecordId == tradeRecordId &&
                       row.Status != nameof(PaymentObligationStatus.PartnerReportedSettled),
                cancellationToken))
        {
            return new TradeClosureResult(TradeClosureStatus.PaymentOutstanding, null);
        }

        if (await dbContext.LogisticsShipments.AnyAsync(
                row => row.TradeRecordId == tradeRecordId &&
                       row.LastMilestone != (int)LogisticsMilestoneType.Delivery,
                cancellationToken))
        {
            return new TradeClosureResult(TradeClosureStatus.LogisticsIncomplete, null);
        }

        int updated;
        if (organizationId == tradeRow.ProducerOrganizationId)
        {
            updated = await dbContext.DirectTradeRecords
                .Where(row =>
                    row.Id == tradeRecordId &&
                    row.Status == nameof(DirectTradeRecordStatus.Confirmed) &&
                    row.ProducerClosedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(row => row.ProducerClosedAtUtc, confirmedAtUtc)
                        .SetProperty(
                            row => row.Status,
                            row => row.BuyerClosedAtUtc == null
                                ? nameof(DirectTradeRecordStatus.Confirmed)
                                : nameof(DirectTradeRecordStatus.Closed)),
                    cancellationToken);
        }
        else
        {
            updated = await dbContext.DirectTradeRecords
                .Where(row =>
                    row.Id == tradeRecordId &&
                    row.Status == nameof(DirectTradeRecordStatus.Confirmed) &&
                    row.BuyerClosedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(row => row.BuyerClosedAtUtc, confirmedAtUtc)
                        .SetProperty(
                            row => row.Status,
                            row => row.ProducerClosedAtUtc == null
                                ? nameof(DirectTradeRecordStatus.Confirmed)
                                : nameof(DirectTradeRecordStatus.Closed)),
                    cancellationToken);
        }

        if (updated == 0)
        {
            var currentRow = await dbContext.DirectTradeRecords
                .AsNoTracking()
                .SingleAsync(row => row.Id == tradeRecordId, cancellationToken);
            return new TradeClosureResult(TradeClosureStatus.AlreadyConfirmed, ToDomain(currentRow));
        }

        var updatedRow = await dbContext.DirectTradeRecords
            .AsNoTracking()
            .SingleAsync(row => row.Id == tradeRecordId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var tradeRecord = ToDomain(updatedRow);
        return new TradeClosureResult(
            tradeRecord.Status == DirectTradeRecordStatus.Closed
                ? TradeClosureStatus.Closed
                : TradeClosureStatus.ConfirmationRecorded,
            tradeRecord);
    }

    private async Task<DirectTradeRow?> GetConfirmedTradeAsync(
        Guid tradeRecordId,
        CancellationToken cancellationToken) =>
        await dbContext.DirectTradeRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == tradeRecordId &&
                       row.Status == nameof(DirectTradeRecordStatus.Confirmed),
                cancellationToken);

    private static bool IsTradeParty(DirectTradeRow trade, Guid organizationId) =>
        organizationId == trade.ProducerOrganizationId || organizationId == trade.BuyerOrganizationId;

    private static ComplianceTaskRow ToRow(ComplianceTask task) =>
        new()
        {
            Id = task.Id,
            TradeRecordId = task.TradeRecordId,
            ResponsibleOrganizationId = task.ResponsibleOrganizationId,
            DocumentType = task.DocumentType,
            Issuer = task.Issuer,
            RequirementSource = task.RequirementSource,
            DueAtUtc = task.DueAtUtc,
            Status = task.Status.ToString(),
            EvidenceReference = task.EvidenceReference
        };

    private static ComplianceTask ToDomain(ComplianceTaskRow row)
    {
        var task = new ComplianceTask(
            row.Id,
            row.TradeRecordId,
            row.ResponsibleOrganizationId,
            row.DocumentType,
            row.Issuer,
            row.RequirementSource,
            row.DueAtUtc);
        if (row.Status is nameof(ComplianceTaskStatus.EvidenceRecorded) or
            nameof(ComplianceTaskStatus.ResponsiblePartyAttested))
        {
            task.RecordEvidence(row.ResponsibleOrganizationId, row.EvidenceReference!);
        }

        if (row.Status == nameof(ComplianceTaskStatus.ResponsiblePartyAttested))
        {
            task.AttestByResponsibleParty(row.ResponsibleOrganizationId);
        }

        return task;
    }

    private static ComplianceTaskView ToView(ComplianceTaskRow row) =>
        new(
            row.Id,
            row.TradeRecordId,
            row.ResponsibleOrganizationId,
            row.DocumentType,
            row.Issuer,
            row.RequirementSource,
            row.DueAtUtc,
            row.Status,
            row.EvidenceReference);

    private static PaymentObligationRow ToRow(PaymentObligation obligation) =>
        new()
        {
            Id = obligation.Id,
            TradeRecordId = obligation.TradeRecordId,
            PayerOrganizationId = obligation.PayerOrganizationId,
            BeneficiaryOrganizationId = obligation.BeneficiaryOrganizationId,
            PaymentPartnerOrganizationId = obligation.PaymentPartnerOrganizationId,
            Amount = obligation.Amount,
            CurrencyCode = obligation.CurrencyCode,
            ProviderName = obligation.ProviderName,
            ProviderReference = obligation.ProviderReference,
            Status = obligation.Status.ToString(),
            PartnerReportedSettledAtUtc = obligation.PartnerReportedSettledAtUtc
        };

    private static PaymentObligation ToDomain(PaymentObligationRow row) =>
        new(
            row.Id,
            row.TradeRecordId,
            row.PayerOrganizationId,
            row.BeneficiaryOrganizationId,
            row.PaymentPartnerOrganizationId,
            row.Amount,
            row.CurrencyCode,
            row.ProviderName,
            row.ProviderReference,
            Enum.Parse<PaymentObligationStatus>(row.Status),
            row.PartnerReportedSettledAtUtc);

    private static LogisticsShipmentView ToView(LogisticsShipmentRow row) =>
        new(row.Id, row.TradeRecordId, row.ResponsibleProviderOrganizationId, row.ShipmentReference);

    private static LogisticsMilestoneRow ToRow(LogisticsMilestone milestone) =>
        new()
        {
            Id = milestone.Id,
            ShipmentId = milestone.ShipmentId,
            ReportedByOrganizationId = milestone.ReportedByOrganizationId,
            Type = milestone.Type.ToString(),
            EvidenceReference = milestone.EvidenceReference,
            RecordedAtUtc = milestone.RecordedAtUtc
        };

    private static LogisticsMilestoneView ToView(LogisticsMilestoneRow row) =>
        new(
            row.Id,
            row.ShipmentId,
            row.ReportedByOrganizationId,
            Enum.Parse<LogisticsMilestoneType>(row.Type),
            row.EvidenceReference,
            row.RecordedAtUtc);

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
}
