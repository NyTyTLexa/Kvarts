using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Notifications;

public class Notification : Entity
{
    public string? RecipientUserName { get; set; }
    public string? RecipientRole { get; set; }
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string DedupeKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? EmailedAtUtc { get; set; }

    public bool IsRead => ReadAtUtc is not null;

    public void MarkRead()
    {
        ReadAtUtc ??= DateTime.UtcNow;
    }

    public void MarkEmailed()
    {
        EmailedAtUtc ??= DateTime.UtcNow;
    }
}
