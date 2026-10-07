using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TheGate.Api;
using TheGate.Application.Trade;
using TheGate.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
});

builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DirectTradeWorkflow>();
builder.Services.AddTradeInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapTradeEndpoints();

app.Run();

public partial class Program
{
}
