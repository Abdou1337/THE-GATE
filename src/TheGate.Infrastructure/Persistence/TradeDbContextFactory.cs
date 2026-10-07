using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TheGate.Infrastructure.Persistence;

public sealed class TradeDbContextFactory : IDesignTimeDbContextFactory<TradeDbContext>
{
    public TradeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TradeDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings__TradeDatabase to generate or apply migrations.");
        }

        var options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TradeDbContext(options);
    }
}
