using System.Security.Claims;
using TheGate.Application.Trade;
using TheGate.Domain.Trade;

namespace TheGate.Api;

public static class TradeEndpoints
{
    public static IEndpointRouteBuilder MapTradeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/offers", async (
                DirectTradeWorkflow workflow,
                CancellationToken cancellationToken) =>
            Results.Ok(await workflow.GetOffersAsync(cancellationToken)))
            .AllowAnonymous();

        api.MapGet("/offers/{offerId:guid}", async (
                Guid offerId,
                DirectTradeWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                var offer = await workflow.GetOfferAsync(offerId, cancellationToken);
                return offer is null ? Results.NotFound() : Results.Ok(offer);
            })
            .AllowAnonymous();

        api.MapPost("/offers", async (
                CreateOfferRequest request,
                ClaimsPrincipal principal,
                DirectTradeWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var producerOrganizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    Uri? externalContactUri = null;
                    if (request.ExternalContactUri is not null &&
                        !Uri.TryCreate(request.ExternalContactUri, UriKind.Absolute, out externalContactUri))
                    {
                        return Results.BadRequest(new { error = "External contact URI must be an absolute HTTPS URI." });
                    }

                    var offer = await workflow.CreateOfferAsync(
                        producerOrganizationId,
                        request.ProductDescription,
                        new Quantity(request.DeclaredQuantity, request.UnitCode),
                        new Quantity(request.MinimumDirectTradeQuantity, request.UnitCode),
                        externalContactUri,
                        cancellationToken);

                    return Results.Created(
                        $"/api/offers/{offer.Id}",
                        new OfferListing(
                            offer.Id,
                            offer.ProducerOrganizationId,
                            offer.ProductDescription,
                            offer.DeclaredQuantity.Value,
                            offer.MinimumDirectTradeQuantity.Value,
                            offer.DeclaredQuantity.Value,
                            offer.DeclaredQuantity.UnitCode,
                            offer.ExternalContactUri));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .RequireAuthorization("producer");

        api.MapPost("/offers/{offerId:guid}/trades", async (
                Guid offerId,
                RegisterTradeRequest request,
                ClaimsPrincipal principal,
                DirectTradeWorkflow workflow,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var buyerOrganizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var result = await workflow.RegisterDirectTradeAsync(
                        offerId,
                        buyerOrganizationId,
                        new Quantity(request.AgreedQuantity, request.UnitCode),
                        timeProvider.GetUtcNow(),
                        cancellationToken);

                    return result.Status switch
                    {
                        TradeRegistrationStatus.OfferNotFound => Results.NotFound(),
                        TradeRegistrationStatus.InsufficientQuantity => Results.Conflict(
                            new { error = "The requested quantity is no longer available." }),
                        TradeRegistrationStatus.Registered => Results.Created(
                            $"/api/trades/{result.TradeRecord!.Id}",
                            TradeRecordResponse.From(result.TradeRecord)),
                        _ => Results.Problem()
                    };
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (InvalidOperationException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .RequireAuthorization("buyer");

        api.MapGet("/trades/{tradeRecordId:guid}", async (
                Guid tradeRecordId,
                ClaimsPrincipal principal,
                DirectTradeWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                var tradeRecord = await workflow.GetTradeRecordAsync(tradeRecordId, cancellationToken);
                if (tradeRecord is null)
                {
                    return Results.NotFound();
                }

                if (!IsTradeParty(principal, tradeRecord))
                {
                    return Results.Forbid();
                }

                return Results.Ok(TradeRecordResponse.From(tradeRecord));
            })
            .RequireAuthorization();

        api.MapPost("/trades/{tradeRecordId:guid}/verifications", async (
                Guid tradeRecordId,
                CreateVerificationRequest request,
                ClaimsPrincipal principal,
                DirectTradeWorkflow workflow,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetOrganizationId(principal, out var verifierOrganizationId))
                {
                    return Results.Forbid();
                }

                try
                {
                    var report = await workflow.AddVerificationAsync(
                        tradeRecordId,
                        verifierOrganizationId,
                        new Quantity(request.MeasuredQuantity, request.UnitCode),
                        request.EvidenceReference,
                        timeProvider.GetUtcNow(),
                        cancellationToken);

                    return Results.Created(
                        $"/api/trades/{tradeRecordId}/verifications",
                        VerificationResponse.From(report));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .RequireAuthorization("inspector");

        api.MapGet("/trades/{tradeRecordId:guid}/verifications", async (
                Guid tradeRecordId,
                ClaimsPrincipal principal,
                DirectTradeWorkflow workflow,
                CancellationToken cancellationToken) =>
            {
                var tradeRecord = await workflow.GetTradeRecordAsync(tradeRecordId, cancellationToken);
                if (tradeRecord is null)
                {
                    return Results.NotFound();
                }

                if (!IsTradeParty(principal, tradeRecord))
                {
                    return Results.Forbid();
                }

                var reports = await workflow.GetVerificationsAsync(tradeRecordId, cancellationToken);
                return Results.Ok(reports.Select(VerificationResponse.From));
            })
            .RequireAuthorization();

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

public sealed record CreateOfferRequest(
    string ProductDescription,
    decimal DeclaredQuantity,
    decimal MinimumDirectTradeQuantity,
    string UnitCode,
    string? ExternalContactUri);

public sealed record RegisterTradeRequest(decimal AgreedQuantity, string UnitCode);

public sealed record CreateVerificationRequest(
    decimal MeasuredQuantity,
    string UnitCode,
    string EvidenceReference);

public sealed record TradeRecordResponse(
    Guid Id,
    Guid OfferId,
    Guid ProducerOrganizationId,
    Guid BuyerOrganizationId,
    decimal AgreedQuantity,
    string UnitCode,
    DateTimeOffset RecordedAtUtc)
{
    public static TradeRecordResponse From(DirectTradeRecord tradeRecord) =>
        new(
            tradeRecord.Id,
            tradeRecord.OfferId,
            tradeRecord.ProducerOrganizationId,
            tradeRecord.BuyerOrganizationId,
            tradeRecord.AgreedQuantity.Value,
            tradeRecord.AgreedQuantity.UnitCode,
            tradeRecord.RecordedAtUtc);
}

public sealed record VerificationResponse(
    Guid Id,
    Guid TradeRecordId,
    Guid VerifierOrganizationId,
    decimal MeasuredQuantity,
    string UnitCode,
    string EvidenceReference,
    DateTimeOffset InspectedAtUtc)
{
    public static VerificationResponse From(IndependentVerificationReport report) =>
        new(
            report.Id,
            report.TradeRecordId,
            report.VerifierOrganizationId,
            report.MeasuredQuantity.Value,
            report.MeasuredQuantity.UnitCode,
            report.EvidenceReference,
            report.InspectedAtUtc);
}
