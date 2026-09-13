using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Audit;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Import;
using ProcurementSystem.Domain.Notifications;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Domain.Outbox;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Domain.Projects;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Domain.Retail;

namespace ProcurementSystem.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<SpecificationItem> SpecificationItems => Set<SpecificationItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<PriceHistoryEntry> PriceHistory => Set<PriceHistoryEntry>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<QuoteOverride> QuoteOverrides => Set<QuoteOverride>();
    public DbSet<PriceImportUpload> PriceImportUploads => Set<PriceImportUpload>();
    public DbSet<InvoiceAttachment> InvoiceAttachments => Set<InvoiceAttachment>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RetailShop> RetailShops => Set<RetailShop>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Vendor>(e =>
        {
            e.Property(v => v.Name).HasMaxLength(300).IsRequired();
            e.Property(v => v.Inn).HasMaxLength(20);
        });

        b.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(100).IsRequired();
            e.Property(p => p.Name).HasMaxLength(500).IsRequired();
            e.Property(p => p.Manufacturer).HasMaxLength(300);
            e.Property(p => p.Category).HasMaxLength(300);
            e.HasIndex(p => p.Sku);
        });

        b.Entity<PriceListItem>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Currency).HasMaxLength(3);
            e.Property(p => p.SourceUrl).HasMaxLength(1000);
            e.HasIndex(p => new { p.ProductId, p.VendorId });
            e.HasOne(p => p.Vendor).WithMany(v => v.PriceListItems).HasForeignKey(p => p.VendorId);
            e.HasOne(p => p.Product).WithMany(p => p.Offers).HasForeignKey(p => p.ProductId);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.Property(o => o.Type).HasMaxLength(300).IsRequired();
            e.HasIndex(o => o.ProcessedAtUtc);   // Р±С‹СЃС‚СЂС‹Р№ РІС‹Р±РѕСЂ РЅРµРѕР±СЂР°Р±РѕС‚Р°РЅРЅС‹С…
        });

        b.Entity<Specification>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(300).IsRequired();
            e.Property(s => s.Customer).HasMaxLength(300);
        });

        b.Entity<SpecificationItem>(e =>
        {
            e.Property(i => i.RawSku).HasMaxLength(100);
            e.Property(i => i.RawName).HasMaxLength(500).IsRequired();
            e.HasOne(i => i.Specification).WithMany(s => s.Items)
                .HasForeignKey(i => i.SpecificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany()
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Order>(e =>
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

        b.Entity<OrderLine>(e =>
        {
            e.Property(l => l.Sku).HasMaxLength(100);
            e.Property(l => l.Name).HasMaxLength(500).IsRequired();
            e.Property(l => l.VendorName).HasMaxLength(300);
            e.Property(l => l.UnitPrice).HasPrecision(18, 2);
            e.Property(l => l.LineTotal).HasPrecision(18, 2);
            e.HasOne(l => l.Order).WithMany(o => o.Lines)
                .HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PriceHistoryEntry>(e =>
        {
            e.Property(h => h.Price).HasPrecision(18, 2);
            e.HasIndex(h => new { h.ProductId, h.RecordedAtUtc });
        });

        b.Entity<AuditEntry>(e =>
        {
            e.Property(a => a.UserName).HasMaxLength(150);
            e.Property(a => a.Action).HasMaxLength(10).IsRequired();
            e.Property(a => a.Path).HasMaxLength(500).IsRequired();
            e.Property(a => a.ChangesJson).HasColumnType("jsonb");
            e.HasIndex(a => a.OccurredAtUtc);
        });

        b.Entity<Discount>(e =>
        {
            e.ToTable("Discounts");
            e.Property(d => d.Manufacturer).HasMaxLength(300);
            e.Property(d => d.Percent).HasPrecision(5, 2);      // 0.00 .. 999.99 (Р±РёР·РЅРµСЃ-РґРёР°РїР°Р·РѕРЅ 0..100)
            e.Property(d => d.CreatedBy).HasMaxLength(150);
            e.HasIndex(d => d.VendorId);
            e.HasIndex(d => d.Manufacturer);
            // РЎРІСЏР·СЊ СЃ РїРѕСЃС‚Р°РІС‰РёРєРѕРј РѕРїС†РёРѕРЅР°Р»СЊРЅР° (СЃРєРёРґРєР° РјРѕР¶РµС‚ Р±С‹С‚СЊ РїРѕ РїСЂРѕРёР·РІРѕРґРёС‚РµР»СЋ Р±РµР· РІРµРЅРґРѕСЂР°).
            e.HasOne(d => d.Vendor).WithMany()
                .HasForeignKey(d => d.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        // РњРѕРґСѓР»Рё РўСЂРµРєРѕРІ A/B (Commercial, Logistics, Integration) РїРѕРґРєР»СЋС‡Р°СЋС‚ СЃРІРѕРё СЃСѓС‰РЅРѕСЃС‚Рё
        // РѕС‚РґРµР»СЊРЅС‹РјРё IEntityTypeConfiguration<T>-С„Р°Р№Р»Р°РјРё вЂ” РёС… РЅРµ РЅСѓР¶РЅРѕ РїСЂР°РІРёС‚СЊ Р·РґРµСЃСЊ.
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

