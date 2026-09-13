using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Retail;

namespace ProcurementSystem.Services.Retail.Persistence;

public class RetailShopConfiguration : IEntityTypeConfiguration<RetailShop>
{
    public void Configure(EntityTypeBuilder<RetailShop> e)
    {
        e.ToTable("RetailShops");
        e.Property(s => s.Host).HasMaxLength(200).IsRequired();
        e.Property(s => s.DisplayName).HasMaxLength(200).IsRequired();
        e.Property(s => s.Kind).HasMaxLength(20).IsRequired();
        e.Property(s => s.SearchUrlTemplate).HasMaxLength(500);
        e.Property(s => s.LastError).HasMaxLength(400);
        e.HasIndex(s => s.Host).IsUnique();
    }
}
