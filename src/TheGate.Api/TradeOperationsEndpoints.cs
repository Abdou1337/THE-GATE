using System.Security.Claims;
using TheGate.Application.Trade;
using TheGate.Domain.Trade;

namespace TheGate.Api;

public static class TradeOperationsEndpoints
{
    public static IEndpointRouteBuilder MapTradeOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/account/me", (ClaimsPrincipal principal) =>
            Results.Ok(new AccountProfileResponse(
                principal.FindFirstValue("sub") ?? string.Empty,
                principal.FindFirstValue("organization_id") ?? string.Empty,
                principal.FindFirstValue("organization_role") ?? string.Empty,
                principal.FindFirstValue("name"),
                principal.FindFirstValue("email"))))
            .RequireAuthorization(policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("sub")
                .RequireClaim("organization_id")
                .RequireClaim("organization_role"));

        api.MapGet("/trades/{tradeRecordId:guid}/compliance-tasks", async (
                Guid tradeRecordId,
                ClaimsPrincipal principal,
                DirectTradeWorkflow tradeWorkflow,
                TradeOperationsWorkflow operationsWorkflow,
                CancellationToken cancellationToken) =>
            {
                var trade = await tradeWorkflow.GetTradeRecordAsync(tradeRecordId, cancellationToken);
                if (trade is null)
                {
                    return Results.NotFound();
                }

                if (!IsTradeParty(principal, trade))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await operationsWorkflow.GetComplianceTasksAsync(tradeRecordId, cancellationToken));
            })
            .RequireAuthorization();

        api.MapPost("/trades/{tradeRecordId:guid}/compliance-tasks", async (
                Guid tradeRecordId,
                CreateComplianceTaskRequest request,
                ClaimsPrincipal principal,
                DirectTradeWorkflow tradeWorkflow,
                TradeOperationsWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var organizationId))
                {
                    return Results.Forbid();
                }

                var trade = await tradeWorkflow.GetTradeRecordAsync(tradeRecordId, cancellationToken);
                if (trade is null)
                {
                    return Results.NotFound();
                }

                if (!IsTradeParty(principal, trade))
                {
                    return Results.Forbid();
                }

                try
                {
                    var task = await workflow.CreateComplianceTaskAsync(
                        tradeRecordId,
                        request.ResponsibleOrganizationId,
                        request.DocumentType,
                        request.Issuer,
                        request.RequirementSource,
                        request.DueAtUtc,
                        cancellationToken);
                    if (task is null)
                    {
                        return Results.NotFound();
                    }

                    return Results.Created($"/api/compliance-tasks/{task.Id}", task);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .RequireAuthorization("trade-party");

        api.MapPost("/compliance-tasks/{taskId:guid}/evidence", async (
                Guid taskId,
                RecordComplianceEvidenceRequest request,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var organizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var task = await workflow.RecordComplianceEvidenceAsync(
                        taskId,
                        organizationId,
                        request.EvidenceReference,
                        cancellationToken);
                    return task is null ? Results.NotFound() : Results.Ok(task);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .RequireAuthorization("trade-party");

        api.MapPost("/compliance-tasks/{taskId:guid}/attest", async (
                Guid taskId,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var organizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var task = await workflow.AttestComplianceTaskAsync(taskId, organizationId, cancellationToken);
                    return task is null ? Results.NotFound() : Results.Ok(task);
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .RequireAuthorization("trade-party");

        api.MapGet("/trades/{tradeRecordId:guid}/payment-obligations", async (
                Guid tradeRecordId,
                ClaimsPrincipal principal,
                DirectTradeWorkflow tradeWorkflow,
                TradeOperationsWorkflow operationsWorkflow,
                CancellationToken cancellationToken) =>
            {
                var trade = await tradeWorkflow.GetTradeRecordAsync(tradeRecordId, cancellationToken);
                if (trade is null)
                {
                    return Results.NotFound();
                }

                if (!IsTradeParty(principal, trade))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await operationsWorkflow.GetPaymentObligationsAsync(tradeRecordId, cancellationToken));
            })
            .RequireAuthorization();

        api.MapPost("/trades/{tradeRecordId:guid}/payment-obligations", async (
                Guid tradeRecordId,
                CreatePaymentObligationRequest request,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var organizationId) ||
                    request.PayerOrganizationId != organizationId &&
                    request.BeneficiaryOrganizationId != organizationId)
                {
                    return Results.Forbid();
                }

                try
                {
                    var obligation = await workflow.CreatePaymentObligationAsync(
                        tradeRecordId,
                        request.PayerOrganizationId,
                        request.BeneficiaryOrganizationId,
                        request.PaymentPartnerOrganizationId,
                        request.Amount,
                        request.CurrencyCode,
                        request.ProviderName,
                        request.ProviderReference,
                        cancellationToken);
                    return obligation is null
                        ? Results.NotFound()
                        : Results.Created($"/api/payment-obligations/{obligation.Id}", obligation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .RequireAuthorization("trade-party");

        api.MapPost("/payment-obligations/{obligationId:guid}/partner-report", async (
                Guid obligationId,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var organizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var obligation = await workflow.RecordPartnerSettlementAsync(
                        obligationId,
                        organizationId,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                    return obligation is null ? Results.NotFound() : Results.Ok(obligation);
                }
                catch (InvalidOperationException)
                {
                    return Results.Forbid();
                }
            })
            .RequireAuthorization("payment-partner");

        api.MapPost("/trades/{tradeRecordId:guid}/shipments", async (
                Guid tradeRecordId,
                CreateShipmentRequest request,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var providerOrganizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var shipment = await workflow.CreateShipmentAsync(
                        tradeRecordId,
                        providerOrganizationId,
                        request.ShipmentReference,
                        cancellationToken);
                    return shipment is null
                        ? Results.NotFound()
                        : Results.Created($"/api/shipments/{shipment.Id}", shipment);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .RequireAuthorization("logistics-provider");

        api.MapPost("/trades/{tradeRecordId:guid}/shipments/{shipmentId:guid}/milestones", async (
                Guid tradeRecordId,
                Guid shipmentId,
                RecordLogisticsMilestoneRequest request,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var providerOrganizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var milestone = await workflow.RecordLogisticsMilestoneAsync(
                        tradeRecordId,
                        shipmentId,
                        providerOrganizationId,
                        request.Type,
                        request.EvidenceReference,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                    return milestone is null
                        ? Results.Conflict(new { error = "Shipment stage is out of order or the trade is not open." })
                        : Results.Created($"/api/shipments/{shipmentId}/milestones/{milestone.Id}", milestone);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (InvalidOperationException)
                {
                    return Results.Forbid();
                }
            })
            .RequireAuthorization("logistics-provider");

        api.MapGet("/trades/{tradeRecordId:guid}/logistics-milestones", async (
                Guid tradeRecordId,
                ClaimsPrincipal principal,
                DirectTradeWorkflow tradeWorkflow,
                TradeOperationsWorkflow operationsWorkflow,
                CancellationToken cancellationToken) =>
            {
                var trade = await tradeWorkflow.GetTradeRecordAsync(tradeRecordId, cancellationToken);
                if (trade is null)
                {
                    return Results.NotFound();
                }

                if (!IsTradeParty(principal, trade))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await operationsWorkflow.GetLogisticsMilestonesAsync(
                    tradeRecordId,
                    cancellationToken));
            })
            .RequireAuthorization();

        api.MapPost("/trades/{tradeRecordId:guid}/closure-confirmations", async (
                Guid tradeRecordId,
                ClaimsPrincipal principal,
                TradeOperationsWorkflow workflow,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var organizationId))
                {
                    return Results.Forbid();
                }

                var result = await workflow.ConfirmTradeClosureAsync(
                    tradeRecordId,
                    organizationId,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                return result.Status switch
                {
                    TradeClosureStatus.TradeRecordNotFound => Results.NotFound(),
                    TradeClosureStatus.NotTradeParty => Results.Forbid(),
                    TradeClosureStatus.NotConfirmed => Results.Conflict(
                        new { error = "Only a producer-confirmed trade can be closed." }),
                    TradeClosureStatus.ComplianceIncomplete => Results.Conflict(
                        new { error = "Every recorded compliance task needs responsible-party attestation." }),
                    TradeClosureStatus.PaymentOutstanding => Results.Conflict(
                        new { error = "Every recorded payment obligation needs a payment-partner report." }),
                    TradeClosureStatus.LogisticsIncomplete => Results.Conflict(
                        new { error = "Every recorded shipment must reach the delivered milestone." }),
                    TradeClosureStatus.AlreadyConfirmed when result.TradeRecord is not null =>
                        Results.Ok(TradeRecordResponse.From(result.TradeRecord)),
                    TradeClosureStatus.AlreadyConfirmed => Results.Conflict(
                        new { error = "This party has already confirmed closure." }),
                    TradeClosureStatus.ConfirmationRecorded or TradeClosureStatus.Closed =>
                        Results.Ok(TradeRecordResponse.From(result.TradeRecord!)),
                    _ => Results.Problem()
                };
            })
            .RequireAuthorization("trade-party");

        return endpoints;
    }

    private static bool TryGetOrganizationId(ClaimsPrincipal principal, out Guid organizationId) =>
        Guid.TryParse(principal.FindFirstValue("organization_id"), out organizationId) &&
        organizationId != Guid.Empty;

    private static bool IsTradeParty(ClaimsPrincipal principal, DirectTradeRecord tradeRecord) =>
        TryGetOrganizationId(principal, out var organizationId) &&
        (organizationId == tradeRecord.ProducerOrganizationId ||
         organizationId == tradeRecord.BuyerOrganizationId);
}

public sealed record AccountProfileResponse(
    string Subject,
    string OrganizationId,
    string OrganizationRole,
    string? DisplayName,
    string? Email);

public sealed record CreateComplianceTaskRequest(
    Guid ResponsibleOrganizationId,
    string DocumentType,
    string Issuer,
    string RequirementSource,
    DateTimeOffset? DueAtUtc);

public sealed record RecordComplianceEvidenceRequest(string EvidenceReference);

public sealed record CreatePaymentObligationRequest(
    Guid PayerOrganizationId,
    Guid BeneficiaryOrganizationId,
    Guid PaymentPartnerOrganizationId,
    decimal Amount,
    string CurrencyCode,
    string ProviderName,
    string ProviderReference);

public sealed record CreateShipmentRequest(string ShipmentReference);

public sealed record RecordLogisticsMilestoneRequest(
    LogisticsMilestoneType Type,
    string EvidenceReference);
