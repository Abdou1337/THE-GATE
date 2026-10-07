using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheGate.Api;
using TheGate.Application.Trade;
using TheGate.Domain.Trade;
using TheGate.Infrastructure.Persistence;

namespace TheGate.Api.Tests;

[CollectionDefinition("Database integration", DisableParallelization = true)]
public sealed class DatabaseIntegrationCollection
{
}

[Collection("Database integration")]
public sealed class ApiWorkflowTests
{
    [Fact]
    public void Jwt_bearer_validation_uses_the_configured_https_issuer_and_audience()
    {
        using var factory = new GateApiFactory();
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://identity.example.test", options.Authority);
        Assert.Equal("https://identity.example.test/", options.TokenValidationParameters.ValidIssuer);
        Assert.Equal("the-gate-api", options.Audience);
        Assert.True(options.RequireHttpsMetadata);
        Assert.Equal("organization_role", options.TokenValidationParameters.RoleClaimType);
    }

    [Fact]
    public async Task Direct_trade_workflow_persists_records_and_enforces_roles_and_quantity_limits()
    {
        using var factory = new GateApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/offers")).StatusCode);

        var anonymousOfferResponse = await client.PostAsJsonAsync(
            "/api/offers",
            new CreateOfferRequest("Coffee", 100m, 50m, "kg", null));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousOfferResponse.StatusCode);

        var producerOrganizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");

        var offerResponse = await client.PostAsJsonAsync(
            "/api/offers",
            new CreateOfferRequest("Coffee", 100m, 50m, "kg", "https://contact.example/producer"));
        Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);
        var offer = await offerResponse.Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(offer);
        Assert.Equal(producerOrganizationId, offer.ProducerOrganizationId);

        var buyerOrganizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        var tradeResponse = await client.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(60m, "kg"));
        Assert.Equal(HttpStatusCode.Created, tradeResponse.StatusCode);
        var trade = await tradeResponse.Content.ReadFromJsonAsync<TradeRecordResponse>();
        Assert.NotNull(trade);
        Assert.Equal(producerOrganizationId, trade.ProducerOrganizationId);
        Assert.Equal(buyerOrganizationId, trade.BuyerOrganizationId);
        Assert.Equal(60m, trade.AgreedQuantity);
        Assert.Equal("AwaitingProducerConfirmation", trade.Status);

        var outsiderOrganizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = factory.BearerToken(outsiderOrganizationId, "producer");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/trades/{trade.Id}/confirm", content: null)).StatusCode);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        var confirmationResponse = await client.PostAsync($"/api/trades/{trade.Id}/confirm", content: null);
        Assert.Equal(HttpStatusCode.OK, confirmationResponse.StatusCode);
        var confirmedTrade = await confirmationResponse.Content.ReadFromJsonAsync<TradeRecordResponse>();
        Assert.NotNull(confirmedTrade);
        Assert.Equal("Confirmed", confirmedTrade.Status);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        var competingTradeResponse = await client.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(60m, "kg"));
        Assert.Equal(HttpStatusCode.Created, competingTradeResponse.StatusCode);
        var competingTrade = await competingTradeResponse.Content.ReadFromJsonAsync<TradeRecordResponse>();
        Assert.NotNull(competingTrade);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/trades/{competingTrade.Id}/confirm", content: null)).StatusCode);

        var refreshedOffer = await (await client.GetAsync($"/api/offers/{offer.Id}"))
            .Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(refreshedOffer);
        Assert.Equal(40m, refreshedOffer.RemainingQuantity);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(outsiderOrganizationId, "buyer");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/trades/{trade.Id}")).StatusCode);

        var inspectorOrganizationId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = factory.BearerToken(inspectorOrganizationId, "inspector");
        var verificationResponse = await client.PostAsJsonAsync(
            $"/api/trades/{trade.Id}/verifications",
            new CreateVerificationRequest(58m, "kg", "evidence-store://reports/abc"));
        Assert.Equal(HttpStatusCode.Created, verificationResponse.StatusCode);
        var verification = await verificationResponse.Content.ReadFromJsonAsync<VerificationResponse>();
        Assert.NotNull(verification);
        Assert.Equal(58m, verification.MeasuredQuantity);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        var verificationList = await (await client.GetAsync($"/api/trades/{trade.Id}/verifications"))
            .Content.ReadFromJsonAsync<VerificationResponse[]>();
        Assert.NotNull(verificationList);
        Assert.Single(verificationList);
        Assert.Equal(58m, verificationList[0].MeasuredQuantity);

        var invalidVerificationResponse = await client.PostAsJsonAsync(
            $"/api/trades/{trade.Id}/verifications",
            new CreateVerificationRequest(60m, "kg", "evidence-store://reports/invalid"));
        Assert.Equal(HttpStatusCode.Forbidden, invalidVerificationResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_offer_and_trade_quantities_are_rejected_without_allocating_stock()
    {
        using var factory = new GateApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(Guid.NewGuid(), "producer");
        var invalidOfferResponse = await client.PostAsJsonAsync(
            "/api/offers",
            new CreateOfferRequest("Coffee", 10m, 11m, "kg", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidOfferResponse.StatusCode);

        var offerResponse = await client.PostAsJsonAsync(
            "/api/offers",
            new CreateOfferRequest("Coffee", 10m, 5m, "kg", null));
        var offer = await offerResponse.Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(offer);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(Guid.NewGuid(), "buyer");
        var belowMinimumResponse = await client.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(4m, "kg"));
        Assert.Equal(HttpStatusCode.BadRequest, belowMinimumResponse.StatusCode);

        var remainingOffer = await (await client.GetAsync($"/api/offers/{offer.Id}"))
            .Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(remainingOffer);
        Assert.Equal(10m, remainingOffer.RemainingQuantity);
    }

    [Fact]
    public async Task Compliance_logistics_payment_and_mutual_closure_follow_the_recorded_responsibilities()
    {
        using var factory = new GateApiFactory(
            Environment.GetEnvironmentVariable("THE_GATE_TEST_POSTGRES_CONNECTION"));
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var producerOrganizationId = Guid.NewGuid();
        var buyerOrganizationId = Guid.NewGuid();
        var logisticsOrganizationId = Guid.NewGuid();
        var paymentPartnerOrganizationId = Guid.NewGuid();

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        var offerResponse = await client.PostAsJsonAsync(
            "/api/offers",
            new CreateOfferRequest("Cocoa", 100m, 20m, "kg", null));
        var offer = await offerResponse.Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(offer);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        var tradeResponse = await client.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(40m, "kg"));
        var trade = await tradeResponse.Content.ReadFromJsonAsync<TradeRecordResponse>();
        Assert.NotNull(trade);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/trades/{trade.Id}/confirm", content: null)).StatusCode);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        var profile = await (await client.GetAsync("/api/account/me"))
            .Content.ReadFromJsonAsync<AccountProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(buyerOrganizationId.ToString(), profile.OrganizationId);
        Assert.Equal("buyer", profile.OrganizationRole);

        var taskResponse = await client.PostAsJsonAsync(
            $"/api/trades/{trade.Id}/compliance-tasks",
            new CreateComplianceTaskRequest(
                producerOrganizationId,
                "Certificate of origin",
                "Competent authority (declared)",
                "Requirement selected by the trade parties; not regulatory advice.",
                null));
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        var task = await taskResponse.Content.ReadFromJsonAsync<ComplianceTaskView>();
        Assert.NotNull(task);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsync(
                $"/api/trades/{trade.Id}/closure-confirmations",
                content: null)).StatusCode);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(Guid.NewGuid(), "producer");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(
                $"/api/compliance-tasks/{task.Id}/evidence",
                new RecordComplianceEvidenceRequest("documents://origin/unauthorized"))).StatusCode);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                $"/api/compliance-tasks/{task.Id}/evidence",
                new RecordComplianceEvidenceRequest("documents://origin/abc"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync($"/api/compliance-tasks/{task.Id}/attest", content: null)).StatusCode);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(logisticsOrganizationId, "logistics_provider");
        var shipmentResponse = await client.PostAsJsonAsync(
            $"/api/trades/{trade.Id}/shipments",
            new CreateShipmentRequest("provider-reference-001"));
        Assert.Equal(HttpStatusCode.Created, shipmentResponse.StatusCode);
        var shipment = await shipmentResponse.Content.ReadFromJsonAsync<LogisticsShipmentView>();
        Assert.NotNull(shipment);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(
                $"/api/trades/{trade.Id}/shipments/{shipment.Id}/milestones",
                new RecordLogisticsMilestoneRequest(LogisticsMilestoneType.Booking, "provider://booking/1"))).StatusCode);

        foreach (var milestone in Enum.GetValues<LogisticsMilestoneType>())
        {
            Assert.Equal(
                HttpStatusCode.Created,
                (await client.PostAsJsonAsync(
                    $"/api/trades/{trade.Id}/shipments/{shipment.Id}/milestones",
                    new RecordLogisticsMilestoneRequest(milestone, $"provider://shipment/{(int)milestone}"))).StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        var paymentResponse = await client.PostAsJsonAsync(
            $"/api/trades/{trade.Id}/payment-obligations",
            new CreatePaymentObligationRequest(
                buyerOrganizationId,
                producerOrganizationId,
                paymentPartnerOrganizationId,
                1250.50m,
                "XAF",
                "External provider declared by parties",
                "provider-ref-001"));
        Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);
        var obligation = await paymentResponse.Content.ReadFromJsonAsync<PaymentObligation>();
        Assert.NotNull(obligation);
        Assert.Equal(PaymentObligationStatus.Pending, obligation.Status);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsync(
                $"/api/trades/{trade.Id}/closure-confirmations",
                content: null)).StatusCode);

        client.DefaultRequestHeaders.Authorization =
            factory.BearerToken(Guid.NewGuid(), "payment_partner");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsync(
                $"/api/payment-obligations/{obligation.Id}/partner-report",
                content: null)).StatusCode);

        client.DefaultRequestHeaders.Authorization =
            factory.BearerToken(paymentPartnerOrganizationId, "payment_partner");
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsync(
                $"/api/payment-obligations/{obligation.Id}/partner-report",
                content: null)).StatusCode);

        using var producerClosureClient = factory.CreateClient();
        using var buyerClosureClient = factory.CreateClient();
        producerClosureClient.DefaultRequestHeaders.Authorization =
            factory.BearerToken(producerOrganizationId, "producer");
        buyerClosureClient.DefaultRequestHeaders.Authorization =
            factory.BearerToken(buyerOrganizationId, "buyer");
        var closureResponses = await Task.WhenAll(
            producerClosureClient.PostAsync(
                $"/api/trades/{trade.Id}/closure-confirmations",
                content: null),
            buyerClosureClient.PostAsync(
                $"/api/trades/{trade.Id}/closure-confirmations",
                content: null));
        Assert.All(closureResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var closureRecords = await Task.WhenAll(
            closureResponses[0].Content.ReadFromJsonAsync<TradeRecordResponse>(),
            closureResponses[1].Content.ReadFromJsonAsync<TradeRecordResponse>());
        Assert.All(closureRecords, record => Assert.NotNull(record));
        Assert.Contains(closureRecords, record => record!.Status == "Closed");

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        var producerClosure = await client.PostAsync(
            $"/api/trades/{trade.Id}/closure-confirmations",
            content: null);
        Assert.Equal(HttpStatusCode.OK, producerClosure.StatusCode);

        client.DefaultRequestHeaders.Authorization = factory.BearerToken(producerOrganizationId, "producer");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Contains("Découvrez des offres africaines", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Concurrent_trade_requests_cannot_overallocate_a_postgres_offer()
    {
        var connectionString = Environment.GetEnvironmentVariable("THE_GATE_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var factory = new GateApiFactory(connectionString);
        await factory.InitializeDatabaseAsync();
        using var producerClient = factory.CreateClient();
        producerClient.DefaultRequestHeaders.Authorization = factory.BearerToken(Guid.NewGuid(), "producer");
        var offerResponse = await producerClient.PostAsJsonAsync(
            "/api/offers",
            new CreateOfferRequest("Coffee", 100m, 50m, "kg", null));
        Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);
        var offer = await offerResponse.Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(offer);

        var buyerOrganizationId = Guid.NewGuid();
        var secondBuyerOrganizationId = Guid.NewGuid();
        using var firstBuyer = factory.CreateClient();
        using var secondBuyer = factory.CreateClient();
        firstBuyer.DefaultRequestHeaders.Authorization = factory.BearerToken(buyerOrganizationId, "buyer");
        secondBuyer.DefaultRequestHeaders.Authorization = factory.BearerToken(secondBuyerOrganizationId, "buyer");
        var firstTradeResponse = await firstBuyer.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(70m, "kg"));
        var secondTradeResponse = await secondBuyer.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(70m, "kg"));
        Assert.Equal(HttpStatusCode.Created, firstTradeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondTradeResponse.StatusCode);
        var firstTrade = await firstTradeResponse.Content.ReadFromJsonAsync<TradeRecordResponse>();
        var secondTrade = await secondTradeResponse.Content.ReadFromJsonAsync<TradeRecordResponse>();
        Assert.NotNull(firstTrade);
        Assert.NotNull(secondTrade);

        using var producerConfirmationClient = factory.CreateClient();
        producerConfirmationClient.DefaultRequestHeaders.Authorization =
            factory.BearerToken(offer.ProducerOrganizationId, "producer");
        var responses = await Task.WhenAll(
            producerConfirmationClient.PostAsync($"/api/trades/{firstTrade.Id}/confirm", content: null),
            producerConfirmationClient.PostAsync($"/api/trades/{secondTrade.Id}/confirm", content: null));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var parts = authorization["Bearer ".Length..].Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var organizationId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid integration-test identity."));
        }

        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("organization_id", organizationId.ToString()),
            new Claim("organization_role", parts[0])
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name, "sub", "organization_role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal sealed class GateApiFactory : WebApplicationFactory<Program>
{
    private readonly string? _postgresConnectionString;
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"the-gate-{Guid.NewGuid():N}.db");

    public GateApiFactory(string? postgresConnectionString = null)
    {
        _postgresConnectionString = postgresConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TradeDatabase"] = "Host=localhost;Database=tests;Username=tests",
                ["Authentication:Jwt:Authority"] = "https://identity.example.test",
                ["Authentication:Jwt:Issuer"] = "https://identity.example.test/",
                ["Authentication:Jwt:Audience"] = "the-gate-api"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });
            services.RemoveAll<TradeDbContext>();
            services.RemoveAll<DbContextOptions<TradeDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TradeDbContext>>();
            services.AddDbContext<TradeDbContext>(options =>
            {
                if (_postgresConnectionString is null)
                {
                    options.UseSqlite($"Data Source={_databasePath};Default Timeout=30");
                }
                else
                {
                    options.UseNpgsql(_postgresConnectionString);
                }
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradeDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public AuthenticationHeaderValue BearerToken(Guid organizationId, string role)
    {
        return new AuthenticationHeaderValue("Bearer", $"{role}.{organizationId}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
