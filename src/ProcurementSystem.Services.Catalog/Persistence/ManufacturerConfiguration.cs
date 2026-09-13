using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Catalog;

namespace ProcurementSystem.Services.Catalog.Persistence;

/// <summary>EF-маппинг справочника производителей. Подхватывается ApplyConfigurationsFromAssembly.</summary>
public class ManufacturerConfiguration : IEntityTypeConfiguration<Manufacturer>
{
    public void Configure(EntityTypeBuilder<Manufacturer> e)
    {
        e.ToTable("Manufacturers");
        e.Property(m => m.Name).HasMaxLength(300).IsRequired();
        e.Property(m => m.Country).HasMaxLength(150);
        e.HasIndex(m => m.Name).IsUnique();
    }
}
