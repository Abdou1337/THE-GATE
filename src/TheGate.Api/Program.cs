using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TheGate.Api;
using TheGate.Application.Trade;
using TheGate.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = DevelopmentSessionEndpoints.Scheme;
            options.DefaultChallengeScheme = DevelopmentSessionEndpoints.Scheme;
            options.DefaultSignInScheme = DevelopmentSessionEndpoints.Scheme;
        })
        .AddCookie(DevelopmentSessionEndpoints.Scheme, options =>
        {
            options.Cookie.Name = "the-gate-dev";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var jwt = builder.Configuration.GetSection("Authentication:Jwt");
            var authority = jwt["Authority"];
            var issuer = jwt["Issuer"];
            var audience = jwt["Audience"];

            if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
                !string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Authentication:Jwt:Authority must be an HTTPS OpenID Connect authority.");
            }

            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException("Authentication:Jwt:Issuer and Authentication:Jwt:Audience must be configured.");
            }

            if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) ||
                !string.Equals(issuerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Authentication:Jwt:Issuer must be an HTTPS issuer URI.");
            }

            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "organization_role"
            };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("producer", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("producer")
            .RequireClaim("organization_id"));
    options.AddPolicy("buyer", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("buyer")
            .RequireClaim("organization_id"));
    options.AddPolicy("inspector", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("inspector")
            .RequireClaim("organization_id"));
    options.AddPolicy("trade-party", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("producer", "buyer")
            .RequireClaim("organization_id"));
    options.AddPolicy("logistics-provider", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("logistics_provider")
            .RequireClaim("organization_id"));
    options.AddPolicy("payment-partner", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("payment_partner")
            .RequireClaim("organization_id"));
});

builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DirectTradeWorkflow>();
builder.Services.AddScoped<TradeOperationsWorkflow>();
builder.Services.AddTradeInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment() &&
    string.Equals(app.Configuration["DatabaseProvider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("SQLite is supported only in the Development environment.");
}

await DevelopmentDatabase.InitializeAsync(app);

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapTradeEndpoints();
app.MapTradeOperationsEndpoints();
app.MapDevelopmentSessionEndpoints();

app.Run();

public partial class Program
{
}
