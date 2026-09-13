using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Projects;
using ProcurementSystem.Domain.Quoting;

namespace ProcurementSystem.Infrastructure.Projects;

/// <summary>EF-маппинг проекта. Подхватывается ApplyConfigurationsFromAssembly в AppDbContext.</summary>
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> e)
    {
        e.ToTable("Projects");
        e.Property(p => p.Name).HasMaxLength(300).IsRequired();
        e.Property(p => p.Rp).HasMaxLength(150);
        e.HasIndex(p => p.SpecificationId);
        // FK без навигационного свойства на Specification — модуль Quoting не трогаем.
        e.HasOne<Specification>().WithMany()
            .HasForeignKey(p => p.SpecificationId).OnDelete(DeleteBehavior.SetNull);
    }
}
