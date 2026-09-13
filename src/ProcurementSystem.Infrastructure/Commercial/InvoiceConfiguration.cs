using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Commercial;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>EF-маппинг счёта NOC отдельным IEntityTypeConfiguration.</summary>
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> e)
    {
        e.ToTable("Invoices");
        e.Property(i => i.Number).HasMaxLength(40).IsRequired();
        e.Property(i => i.ApprovalId).IsRequired();
        e.Property(i => i.Customer).HasMaxLength(300);
        e.Property(i => i.Contract).HasMaxLength(300);
        e.Property(i => i.CostPrice).HasPrecision(18, 2);
        e.Property(i => i.MarkupPercent).HasPrecision(18, 2);
        e.Property(i => i.SellPrice).HasPrecision(18, 2);
        e.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
        e.Property(i => i.CreatedBy).HasMaxLength(150);
        e.HasIndex(i => i.Number);
        e.HasIndex(i => i.ApprovalId);
        // Связь с согласованием (principal — Approval, навигаций нет): FK для целостности, без каскада.
        e.HasOne<Approval>().WithMany().HasForeignKey(i => i.ApprovalId).OnDelete(DeleteBehavior.NoAction);
    }
}
