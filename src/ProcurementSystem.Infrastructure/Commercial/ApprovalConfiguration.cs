using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Commercial;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>
/// EF-маппинг согласования КП отдельным IEntityTypeConfiguration — подхватывается
/// ApplyConfigurationsFromAssembly в AppDbContext, общий файл править не нужно.
/// </summary>
public class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> e)
    {
        e.ToTable("Approvals");
        e.Property(a => a.SpecificationId).IsRequired();
        e.Property(a => a.Strategy).HasMaxLength(40).IsRequired();
        e.Property(a => a.Title).HasMaxLength(300).IsRequired();
        e.Property(a => a.Customer).HasMaxLength(300);
        e.Property(a => a.CostPrice).HasPrecision(18, 2);
        e.Property(a => a.MarkupPercent).HasPrecision(18, 2);
        e.Property(a => a.SellPrice).HasPrecision(18, 2);
        e.Property(a => a.MarginPercent).HasPrecision(18, 2);
        e.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);
        e.Property(a => a.CreatedBy).HasMaxLength(150);
        e.Property(a => a.Comment).HasMaxLength(1000);
        e.HasIndex(a => a.SpecificationId);
    }
}
