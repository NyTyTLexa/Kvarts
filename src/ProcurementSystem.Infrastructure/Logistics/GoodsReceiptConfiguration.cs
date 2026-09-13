using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Logistics;

namespace ProcurementSystem.Infrastructure.Logistics;

/// <summary>EF-конфигурация приёмки. Подхватывается ApplyConfigurationsFromAssembly в AppDbContext.</summary>
public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> e)
    {
        e.ToTable("GoodsReceipts");
        e.Property(r => r.OrderNumber).HasMaxLength(50).IsRequired();
        e.Property(r => r.Customer).HasMaxLength(300);
        e.Property(r => r.CreatedBy).HasMaxLength(150);
        e.Property(r => r.WarehouseRef).HasMaxLength(100);
        e.Property(r => r.AccountingRef).HasMaxLength(100);
        e.HasIndex(r => r.OrderId);
        e.HasMany(r => r.Lines).WithOne(l => l.Receipt)
            .HasForeignKey(l => l.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> e)
    {
        e.ToTable("GoodsReceiptLines");
        e.Property(l => l.Sku).HasMaxLength(100);
        e.Property(l => l.Name).HasMaxLength(500).IsRequired();
        e.Property(l => l.UnitPrice).HasPrecision(18, 2);
        e.Ignore(l => l.Discrepancy);   // вычисляемое свойство — не колонка
    }
}
