using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Notifications;
using ProcurementSystem.Services.Notifications.Persistence;

namespace ProcurementSystem.Services.Notifications.Notifications;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    bool IsRead);

public record NotificationCountDto(int Unread);

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(string? userName, IReadOnlyCollection<string> roles, bool unreadOnly, CancellationToken ct = default);
    Task<NotificationCountDto> CountAsync(string? userName, IReadOnlyCollection<string> roles, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid id, string? userName, IReadOnlyCollection<string> roles, CancellationToken ct = default);
    Task MarkAllReadAsync(string? userName, IReadOnlyCollection<string> roles, CancellationToken ct = default);
    /// <summary>Подтянуть системные уведомления из открытых документов (без фильтра по пользователю).</summary>
    Task SyncAsync(CancellationToken ct = default);
}

public class NotificationService(NotificationsDbContext db, IOpenWorkSource openWork) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(string? userName, IReadOnlyCollection<string> roles, bool unreadOnly, CancellationToken ct = default)
    {
        await TrySyncAsync(ct);
        var query = VisibleTo(userName, roles);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);
        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        return rows.Select(n => n.ToDto()).ToList();
    }

    public async Task<NotificationCountDto> CountAsync(string? userName, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        await TrySyncAsync(ct);
        var count = await VisibleTo(userName, roles).CountAsync(n => n.ReadAtUtc == null, ct);
        return new NotificationCountDto(count);
    }

    public async Task<bool> MarkReadAsync(Guid id, string? userName, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var n = await VisibleTo(userName, roles).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return false;
        n.MarkRead();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task MarkAllReadAsync(string? userName, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var items = await VisibleTo(userName, roles).Where(n => n.ReadAtUtc == null).ToListAsync(ct);
        foreach (var n in items) n.MarkRead();
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Notification> VisibleTo(string? userName, IReadOnlyCollection<string> roles)
    {
        var roleList = roles.ToArray();
        if (roleList.Contains("admin")) return db.Notifications;
        return db.Notifications.Where(n =>
            (n.RecipientUserName == null && n.RecipientRole == null)
            || (userName != null && n.RecipientUserName == userName)
            || (n.RecipientRole != null && roleList.Contains(n.RecipientRole)));
    }

    public Task SyncAsync(CancellationToken ct = default) => SyncSystemNotificationsAsync(ct);

    private async Task TrySyncAsync(CancellationToken ct)
    {
        try { await SyncSystemNotificationsAsync(ct); }
        catch { /* колокол не должен ронять кабинет, если таблица ещё не накатилась */ }
    }

    private async Task SyncSystemNotificationsAsync(CancellationToken ct)
    {
        var existing = await db.Notifications.Select(n => n.DedupeKey).ToHashSetAsync(ct);
        var created = false;

        foreach (var item in await openWork.GetOpenWorkAsync(ct))
            created |= AddIfMissing(existing, item.Type, item.Role, item.Title, item.Message, item.EntityType, item.EntityId, item.CreatedAtUtc);

        if (created) await db.SaveChangesAsync(ct);
    }

    private bool AddIfMissing(HashSet<string> existing, string type, string role, string title, string message, string entityType, Guid entityId, DateTime createdAtUtc)
    {
        var dedupe = $"{type}:{entityId}";
        if (!existing.Add(dedupe)) return false;
        db.Notifications.Add(new Notification
        {
            RecipientRole = role,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityType = entityType,
            RelatedEntityId = entityId,
            DedupeKey = dedupe,
            CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc
                ? createdAtUtc
                : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        });
        return true;
    }
}

internal static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification n) => new(
        n.Id,
        n.Type,
        n.Title,
        n.Message,
        n.RelatedEntityType,
        n.RelatedEntityId,
        n.CreatedAtUtc,
        n.ReadAtUtc,
        n.IsRead);
}
