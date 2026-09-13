using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcurementSystem.Domain.Notifications;

namespace ProcurementSystem.Services.Notifications.Persistence;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.ToTable("Notifications");
        e.Property(n => n.RecipientUserName).HasMaxLength(150);
        e.Property(n => n.RecipientRole).HasMaxLength(80);
        e.Property(n => n.Type).HasMaxLength(80).IsRequired();
        e.Property(n => n.Title).HasMaxLength(200).IsRequired();
        e.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        e.Property(n => n.RelatedEntityType).HasMaxLength(80);
        e.Property(n => n.DedupeKey).HasMaxLength(300).IsRequired();
        e.HasIndex(n => n.DedupeKey).IsUnique();
        e.HasIndex(n => n.EmailedAtUtc);
        e.HasIndex(n => new { n.RecipientUserName, n.ReadAtUtc, n.CreatedAtUtc });
        e.HasIndex(n => new { n.RecipientRole, n.ReadAtUtc, n.CreatedAtUtc });
    }
}
