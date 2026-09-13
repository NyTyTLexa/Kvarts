using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Ordering;

namespace ProcurementSystem.Services.Ordering.Persistence;

public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.Number).HasMaxLength(40).IsRequired();
            e.Property(o => o.Title).HasMaxLength(300).IsRequired();
            e.Property(o => o.Customer).HasMaxLength(300);
            e.Property(o => o.Strategy).HasMaxLength(40).IsRequired();
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.TotalCost).HasPrecision(18, 2);
            e.Property(o => o.CreatedBy).HasMaxLength(150);
            e.HasIndex(o => o.Number);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.Property(l => l.Sku).HasMaxLength(100);
            e.Property(l => l.Name).HasMaxLength(500).IsRequired();
            e.Property(l => l.VendorName).HasMaxLength(300);
            e.Property(l => l.UnitPrice).HasPrecision(18, 2);
            e.Property(l => l.LineTotal).HasPrecision(18, 2);
            e.HasOne(l => l.Order).WithMany(o => o.Lines)
                .HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
