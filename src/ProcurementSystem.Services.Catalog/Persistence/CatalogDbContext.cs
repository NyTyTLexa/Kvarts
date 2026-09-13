using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Audit;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Import;
using ProcurementSystem.Domain.Outbox;
using ProcurementSystem.Domain.Pricing;

namespace ProcurementSystem.Services.Catalog.Persistence;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<PriceHistoryEntry> PriceHistory => Set<PriceHistoryEntry>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<PriceImportUpload> PriceImportUploads => Set<PriceImportUpload>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditEntries", "public", table => table.ExcludeFromMigrations());
            e.Property(a => a.UserName).HasMaxLength(150);
            e.Property(a => a.Action).HasMaxLength(10).IsRequired();
            e.Property(a => a.Path).HasMaxLength(500).IsRequired();
            e.Property(a => a.ChangesJson).HasColumnType("jsonb");
            e.HasIndex(a => a.OccurredAtUtc);
        });

        modelBuilder.Entity<Vendor>(e =>
        {
            e.Property(v => v.Name).HasMaxLength(300).IsRequired();
            e.Property(v => v.Inn).HasMaxLength(20);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(100).IsRequired();
            e.Property(p => p.Name).HasMaxLength(500).IsRequired();
            e.Property(p => p.Manufacturer).HasMaxLength(300);
            e.Property(p => p.Category).HasMaxLength(1000);
            e.HasIndex(p => p.Sku);
        });

        modelBuilder.Entity<PriceListItem>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Currency).HasMaxLength(3);
            e.Property(p => p.SourceUrl).HasMaxLength(1000);
            e.HasIndex(p => new { p.ProductId, p.VendorId });
            e.HasOne(p => p.Vendor).WithMany(v => v.PriceListItems).HasForeignKey(p => p.VendorId);
            e.HasOne(p => p.Product).WithMany(p => p.Offers).HasForeignKey(p => p.ProductId);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.Property(o => o.Type).HasMaxLength(300).IsRequired();
            e.HasIndex(o => o.ProcessedAtUtc);   // быстрый выбор необработанных
        });

        modelBuilder.Entity<PriceHistoryEntry>(e =>
        {
            e.Property(h => h.Price).HasPrecision(18, 2);
            e.HasIndex(h => new { h.ProductId, h.RecordedAtUtc });
        });

        modelBuilder.Entity<Discount>(e =>
        {
            e.ToTable("Discounts");
            e.Property(d => d.Manufacturer).HasMaxLength(300);
            e.Property(d => d.Percent).HasPrecision(5, 2);      // 0.00 .. 999.99 (бизнес-диапазон 0..100)
            e.Property(d => d.CreatedBy).HasMaxLength(150);
            e.HasIndex(d => d.VendorId);
            e.HasIndex(d => d.Manufacturer);
            // Связь с поставщиком опциональна (скидка может быть по производителю без вендора).
            e.HasOne(d => d.Vendor).WithMany()
                .HasForeignKey(d => d.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        // Manufacturer и PriceImportUpload подключаются отдельными IEntityTypeConfiguration<T>-файлами.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
