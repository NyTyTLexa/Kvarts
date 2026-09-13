using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Commercial;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>EF-маппинг строк счёта NOC. Подхватывается ApplyConfigurationsFromAssembly.</summary>
public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> e)
    {
        e.ToTable("InvoiceLines");
        e.Property(l => l.Sku).HasMaxLength(100);
        e.Property(l => l.Name).HasMaxLength(500).IsRequired();
        e.Property(l => l.VendorName).HasMaxLength(300);
        e.Property(l => l.Manufacturer).HasMaxLength(300);
        e.Property(l => l.UnitCost).HasPrecision(18, 2);
        e.Property(l => l.UnitPrice).HasPrecision(18, 2);
        e.Property(l => l.LineTotal).HasPrecision(18, 2);
        e.HasIndex(l => l.InvoiceId);
        e.HasOne(l => l.Invoice).WithMany(i => i.Lines)
            .HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}
