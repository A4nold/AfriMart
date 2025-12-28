using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService.Infrastructure.Data
{
    public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
    {
        public AuthDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();

            // Use your real connection string here
            optionsBuilder.UseNpgsql(
                "Host=localhost;Port=5432;Database=authdb;Username=postgres;Password=password");

            return new AuthDbContext(optionsBuilder.Options);
        }
    }
}