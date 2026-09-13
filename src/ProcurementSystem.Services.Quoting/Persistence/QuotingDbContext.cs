using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Quoting;

namespace ProcurementSystem.Services.Quoting.Persistence;

public class QuotingDbContext(DbContextOptions<QuotingDbContext> options) : DbContext(options)
{
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<SpecificationItem> SpecificationItems => Set<SpecificationItem>();
    public DbSet<QuoteOverride> QuoteOverrides => Set<QuoteOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("quoting");
        modelBuilder.Ignore<Product>();

        modelBuilder.Entity<Specification>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(300).IsRequired();
            e.Property(s => s.Customer).HasMaxLength(300);
        });

        modelBuilder.Entity<SpecificationItem>(e =>
        {
            e.Property(i => i.RawSku).HasMaxLength(100);
            e.Property(i => i.RawName).HasMaxLength(500).IsRequired();
            e.HasOne(i => i.Specification).WithMany(s => s.Items)
                .HasForeignKey(i => i.SpecificationId).OnDelete(DeleteBehavior.Cascade);
            // Product живёт в Catalog: голый Guid, без FK и без Include.
            e.Ignore(i => i.Product);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuotingDbContext).Assembly);
    }
}
