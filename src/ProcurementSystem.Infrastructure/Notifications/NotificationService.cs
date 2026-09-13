using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Domain.Notifications;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Notifications;

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

public class NotificationService(AppDbContext db) : INotificationService
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

        // Только колонки БД: IsDecided / IsDraft / Enum.ToString() Npgsql в SQL не переводит.
        var openApproval = new[] { ApprovalStatus.НаСогласованииРП, ApprovalStatus.ВКоммерческомБлоке };
        var approvals = await db.Set<Approval>()
            .AsNoTracking()
            .Where(a => openApproval.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);
        foreach (var a in approvals)
            created |= AddIfMissing(existing, "approval", "commercial", "КП ожидает согласования", $"{a.Title}: требуется решение коммерческого блока.", "Approval", a.Id, a.CreatedAtUtc);

        var invoices = await db.Set<Invoice>()
            .AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.ОтраженоВ1С && i.Status != InvoiceStatus.Отменён)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);
        foreach (var i in invoices)
            created |= AddIfMissing(existing, "invoice", "accounting", "Счет NOC требует внимания", $"{i.Number}: текущий статус {i.Status}.", "Invoice", i.Id, i.CreatedAtUtc);

        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);
        foreach (var o in orders)
            created |= AddIfMissing(existing, "order", "manager", "Заказ в работе", $"{o.Number}: {o.Title}, статус {o.Status}.", "Order", o.Id, o.CreatedAtUtc);

        var receipts = await db.Set<GoodsReceipt>()
            .AsNoTracking()
            .Where(r => r.Status == ReceiptStatus.Черновик)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);
        foreach (var r in receipts)
            created |= AddIfMissing(existing, "warehouse", "warehouse", "Приемка ожидает проведения", $"Заказ {r.OrderNumber}: нужно сверить складскую приемку.", "GoodsReceipt", r.Id, r.CreatedAtUtc);

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


