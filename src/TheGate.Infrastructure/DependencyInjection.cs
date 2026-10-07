using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheGate.Application.Trade;
using TheGate.Infrastructure.Persistence;

namespace TheGate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TradeDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("TradeDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:TradeDatabase must be configured.");
            }

            var databaseProvider = configuration["DatabaseProvider"];
            if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else if (string.IsNullOrWhiteSpace(databaseProvider) ||
                     string.Equals(databaseProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
            }
        });
        services.AddScoped<ITradeRepository, EfTradeRepository>();
        services.AddScoped<ITradeOperationsRepository, EfTradeOperationsRepository>();
        return services;
    }
}
