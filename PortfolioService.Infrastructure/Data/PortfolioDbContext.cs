using Microsoft.EntityFrameworkCore;
using MarketService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioService.Domain.Data
{
    public class PortfolioDbContext : DbContext
    {
        public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options)
        {
        }

        public DbSet<Market> Markets => Set<Market>();
        public DbSet<MarketOutcome> MarketOutcomes => Set<MarketOutcome>();
        public DbSet<MarketPosition> MarketPositions => Set<MarketPosition>();
        public DbSet<MarketResolution> MarketResolutions => Set<MarketResolution>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
