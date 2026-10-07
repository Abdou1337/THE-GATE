using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
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

        var competingTradeResponse = await client.PostAsJsonAsync(
            $"/api/offers/{offer.Id}/trades",
            new RegisterTradeRequest(60m, "kg"));
        Assert.Equal(HttpStatusCode.Conflict, competingTradeResponse.StatusCode);

        var refreshedOffer = await (await client.GetAsync($"/api/offers/{offer.Id}"))
            .Content.ReadFromJsonAsync<OfferListing>();
        Assert.NotNull(refreshedOffer);
        Assert.Equal(40m, refreshedOffer.RemainingQuantity);

        var outsiderOrganizationId = Guid.NewGuid();
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

        using var firstBuyer = factory.CreateClient();
        using var secondBuyer = factory.CreateClient();
        firstBuyer.DefaultRequestHeaders.Authorization = factory.BearerToken(Guid.NewGuid(), "buyer");
        secondBuyer.DefaultRequestHeaders.Authorization = factory.BearerToken(Guid.NewGuid(), "buyer");
        var responses = await Task.WhenAll(
            firstBuyer.PostAsJsonAsync(
                $"/api/offers/{offer.Id}/trades",
                new RegisterTradeRequest(70m, "kg")),
            secondBuyer.PostAsJsonAsync(
                $"/api/offers/{offer.Id}/trades",
                new RegisterTradeRequest(70m, "kg")));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
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
                ["Authentication:Jwt:Issuer"] = "the-gate-tests",
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
