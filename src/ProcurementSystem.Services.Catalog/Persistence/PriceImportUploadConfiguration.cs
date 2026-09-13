using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Import;

namespace ProcurementSystem.Services.Catalog.Persistence;

/// <summary>EF-маппинг архива загрузок прайсов. Подхватывается ApplyConfigurationsFromAssembly.</summary>
public class PriceImportUploadConfiguration : IEntityTypeConfiguration<PriceImportUpload>
{
    public void Configure(EntityTypeBuilder<PriceImportUpload> e)
    {
        e.ToTable("PriceImportUploads");
        e.Property(u => u.VendorName).HasMaxLength(300).IsRequired();
        e.Property(u => u.FileName).HasMaxLength(260).IsRequired();
        e.Property(u => u.UploadedBy).HasMaxLength(150);
        e.Property(u => u.ErrorsSummary).HasMaxLength(2000);
        e.HasIndex(u => u.VendorId);
        e.HasIndex(u => u.CreatedAtUtc);
        e.HasOne(u => u.Vendor).WithMany()
            .HasForeignKey(u => u.VendorId).OnDelete(DeleteBehavior.Cascade);
    }
}
