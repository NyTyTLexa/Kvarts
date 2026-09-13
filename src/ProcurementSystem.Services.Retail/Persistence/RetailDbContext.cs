using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Retail;

namespace ProcurementSystem.Services.Retail.Persistence;

public class RetailDbContext(DbContextOptions<RetailDbContext> options) : DbContext(options)
{
    public DbSet<RetailShop> RetailShops => Set<RetailShop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("retail");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetailDbContext).Assembly);
    }
}
