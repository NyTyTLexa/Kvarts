using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Commercial;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>EF-маппинг прикреплённых документов счёта. Подхватывается ApplyConfigurationsFromAssembly.</summary>
public class InvoiceAttachmentConfiguration : IEntityTypeConfiguration<InvoiceAttachment>
{
    public void Configure(EntityTypeBuilder<InvoiceAttachment> e)
    {
        e.ToTable("InvoiceAttachments");
        e.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        e.Property(a => a.ContentType).HasMaxLength(200).IsRequired();
        e.Property(a => a.UploadedBy).HasMaxLength(150);
        e.HasIndex(a => a.InvoiceId);
        e.HasIndex(a => a.CreatedAtUtc);
        e.HasOne(a => a.Invoice).WithMany()
            .HasForeignKey(a => a.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}
