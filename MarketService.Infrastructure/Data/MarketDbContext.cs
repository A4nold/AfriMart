using MarketService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketService.Infrastructure.Data;

public class MarketDbContext : DbContext
{
    public MarketDbContext(DbContextOptions<MarketDbContext> options) : base(options) { }

    public DbSet<Market> Markets => Set<Market>();
    public DbSet<MarketOutcome> MarketOutcomes => Set<MarketOutcome>();
    public DbSet<MarketAction> MarketActions => Set<MarketAction>();
    public DbSet<MarketResolution> MarketResolutions => Set<MarketResolution>();
    public DbSet<UserMarketPosition> UserMarketPositions => Set<UserMarketPosition>();
    
    // public DbSet<MarketPosition> MarketPositions => Set<MarketPosition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Applies MarketConfig, or any other config added.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketDbContext).Assembly);
    }
}