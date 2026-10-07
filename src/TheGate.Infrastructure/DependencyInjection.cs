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

            options.UseNpgsql(connectionString);
        });
        services.AddScoped<ITradeRepository, EfTradeRepository>();
        return services;
    }
}
