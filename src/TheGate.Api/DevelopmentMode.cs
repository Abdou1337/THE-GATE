using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TheGate.Infrastructure.Persistence;

namespace TheGate.Api;

public static class DevelopmentSessionEndpoints
{
    public const string Scheme = "DevelopmentCookie";

    private static readonly IReadOnlyDictionary<string, (Guid OrganizationId, string DisplayName)> Personas =
        new Dictionary<string, (Guid, string)>(StringComparer.Ordinal)
        {
            ["producer"] = (Guid.Parse("10000000-0000-4000-8000-000000000001"), "Producteur de démonstration"),
            ["buyer"] = (Guid.Parse("20000000-0000-4000-8000-000000000002"), "Acheteur de démonstration"),
            ["inspector"] = (Guid.Parse("30000000-0000-4000-8000-000000000003"), "Inspecteur de démonstration"),
            ["logistics_provider"] = (Guid.Parse("40000000-0000-4000-8000-000000000004"), "Partenaire logistique de démonstration"),
            ["payment_partner"] = (Guid.Parse("50000000-0000-4000-8000-000000000005"), "Partenaire de paiement de démonstration")
        };

    public static IEndpointRouteBuilder MapDevelopmentSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            endpoints.MapPost("/api/dev/session", async (
                    DevelopmentSessionRequest request,
                    HttpContext context) =>
                {
                    if (!Personas.TryGetValue(request.Role, out var persona))
                    {
                        return Results.BadRequest(new { error = "Choisissez un profil de démonstration disponible." });
                    }

                    var identity = new ClaimsIdentity(
                        [
                            new Claim("sub", $"demo-{request.Role}"),
                            new Claim("organization_id", persona.OrganizationId.ToString()),
                            new Claim("organization_role", request.Role),
                            new Claim("name", persona.DisplayName),
                            new Claim("email", $"demo+{request.Role}@the-gate.local")
                        ],
                        Scheme,
                        "sub",
                        "organization_role");
                    await context.SignInAsync(
                        Scheme,
                        new ClaimsPrincipal(identity),
                        new AuthenticationProperties { IsPersistent = true });
                    return Results.Ok(new DevelopmentSessionResponse(
                        persona.DisplayName,
                        persona.OrganizationId,
                        request.Role));
                })
                .AllowAnonymous();

            endpoints.MapDelete("/api/dev/session", async (HttpContext context) =>
            {
                await context.SignOutAsync(Scheme);
                return Results.NoContent();
            }).AllowAnonymous();
        }

        return endpoints;
    }
}

public static class DevelopmentDatabase
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() ||
            !string.Equals(app.Configuration["DatabaseProvider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connectionString = app.Configuration.GetConnectionString("TradeDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:TradeDatabase must be configured.");
        var connectionBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!connectionBuilder.TryGetValue("Data Source", out var configuredPath) ||
            string.Equals(Convert.ToString(configuredPath), ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Development SQLite requires a file-backed Data Source.");
        }

        var databasePath = Convert.ToString(configuredPath)
            ?? throw new InvalidOperationException("Development SQLite Data Source is invalid.");
        var absolutePath = Path.GetFullPath(databasePath, app.Environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        connectionBuilder["Data Source"] = absolutePath;
        app.Configuration["ConnectionStrings:TradeDatabase"] = connectionBuilder.ConnectionString;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradeDbContext>();
        await db.Database.EnsureCreatedAsync();
        if (await db.ProductOffers.AnyAsync())
        {
            return;
        }

        db.ProductOffers.AddRange(
            CreateDemoOffer(
                "10000000-0000-4000-8000-000000000001",
                "Café arabica — récolte déclarée",
                500m,
                50m,
                "kg",
                "https://example.org/contact/producteur"),
            CreateDemoOffer(
                "10000000-0000-4000-8000-000000000001",
                "Cacao — fèves fermentées déclarées",
                1200m,
                100m,
                "kg",
                null),
            CreateDemoOffer(
                "10000000-0000-4000-8000-000000000001",
                "Beurre de karité — lot disponible",
                250m,
                25m,
                "kg",
                null));
        await db.SaveChangesAsync();
    }

    private static ProductOfferRow CreateDemoOffer(
        string producerOrganizationId,
        string productDescription,
        decimal declaredQuantity,
        decimal minimumDirectTradeQuantity,
        string unitCode,
        string? externalContactUri) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProducerOrganizationId = Guid.Parse(producerOrganizationId),
            ProductDescription = productDescription,
            DeclaredQuantity = declaredQuantity,
            MinimumDirectTradeQuantity = minimumDirectTradeQuantity,
            UnitCode = unitCode,
            ExternalContactUri = externalContactUri
        };
}

public sealed record DevelopmentSessionRequest(string Role);

public sealed record DevelopmentSessionResponse(
    string DisplayName,
    Guid OrganizationId,
    string Role);
