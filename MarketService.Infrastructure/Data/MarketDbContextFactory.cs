using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarketService.Infrastructure.Data
{
    public class MarketDbContextFactory : IDesignTimeDbContextFactory<MarketDbContext>
    {
        public MarketDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MarketDbContext>();

            // Use your real connection string here
            optionsBuilder.UseNpgsql(
                "Host=localhost;Port=5432;Database=marketdb;Username=postgres;Password=password;");

            return new MarketDbContext(optionsBuilder.Options);
        }
    }
}