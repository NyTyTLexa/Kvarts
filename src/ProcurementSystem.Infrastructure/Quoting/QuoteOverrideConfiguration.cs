using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Quoting;

namespace ProcurementSystem.Infrastructure.Quoting;

/// <summary>EF-маппинг ручной корректировки поставщика позиции спецификации (ТЗ п.5.5).
/// Подхватывается ApplyConfigurationsFromAssembly в AppDbContext.</summary>
public class QuoteOverrideConfiguration : IEntityTypeConfiguration<QuoteOverride>
{
    public void Configure(EntityTypeBuilder<QuoteOverride> e)
    {
        e.ToTable("QuoteOverrides");
        e.Property(o => o.UpdatedBy).HasMaxLength(150);
        e.HasIndex(o => o.SpecificationItemId).IsUnique();   // одна корректировка на позицию
        e.HasOne(o => o.SpecificationItem).WithMany()
            .HasForeignKey(o => o.SpecificationItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

