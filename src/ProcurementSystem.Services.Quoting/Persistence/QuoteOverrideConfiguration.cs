using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Quoting;

namespace ProcurementSystem.Services.Quoting.Persistence;

/// <summary>EF-маппинг ручной корректировки поставщика позиции спецификации (ТЗ п.5.5).</summary>
public class QuoteOverrideConfiguration : IEntityTypeConfiguration<QuoteOverride>
{
    public void Configure(EntityTypeBuilder<QuoteOverride> e)
    {
        e.ToTable("QuoteOverrides");
        e.Property(o => o.UpdatedBy).HasMaxLength(150);
        e.HasIndex(o => o.SpecificationItemId).IsUnique();
        e.HasOne(o => o.SpecificationItem).WithMany()
            .HasForeignKey(o => o.SpecificationItemId).OnDelete(DeleteBehavior.Cascade);
        // VendorId — голый Guid: поставщик принадлежит Catalog, FK не ставим.
    }
}
