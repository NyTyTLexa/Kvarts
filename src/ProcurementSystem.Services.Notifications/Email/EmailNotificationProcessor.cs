using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Domain.Notifications;
using ProcurementSystem.Services.Notifications.Notifications;
using ProcurementSystem.Services.Notifications.Persistence;

namespace ProcurementSystem.Services.Notifications.Email;

/// <summary>
/// Один проход рассылки: синхронизирует in-app уведомления (как колокольчик)
/// и отправляет ещё не ушедшие письмом. Бизнес-сервисы не вызывает.
/// </summary>
public sealed class EmailNotificationProcessor(
    NotificationsDbContext db,
    INotificationService notifications,
    IEmailSender email,
    IOptions<EmailOptions> options,
    ILogger<EmailNotificationProcessor> log)
{
    public async Task<int> DispatchOnceAsync(CancellationToken ct = default)
    {
        await notifications.SyncAsync(ct);

        var opts = options.Value;
        if (!opts.Enabled)
            return 0;

        var pending = await db.Notifications
            .Where(n => n.EmailedAtUtc == null)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var n in pending)
        {
            var to = RecipientsFor(opts, n.RecipientRole);
            if (to.Length == 0)
            {
                log.LogWarning(
                    "Email: нет получателей для роли {Role}, уведомление {Id} пропущено",
                    n.RecipientRole, n.Id);
                n.MarkEmailed();
                await db.SaveChangesAsync(ct);
                continue;
            }

            var (subject, body) = Compose(n, opts.FrontendBaseUrl);
            try
            {
                await email.SendAsync(new EmailMessage(to, subject, body), ct);
                n.MarkEmailed();
                await db.SaveChangesAsync(ct);
                sent++;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Email: не удалось отправить «{Subject}» ({Id})", subject, n.Id);
            }
        }

        if (sent > 0)
            log.LogInformation("Email: отправлено {Count} писем", sent);
        return sent;
    }

    internal static string[] RecipientsFor(EmailOptions opts, string? role)
    {
        if (string.IsNullOrWhiteSpace(role) || opts.Recipients.Count == 0)
            return [];

        foreach (var (key, value) in opts.Recipients)
        {
            if (string.Equals(key, role, StringComparison.OrdinalIgnoreCase) && value is { Length: > 0 })
                return value.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        return [];
    }

    internal static (string Subject, string Body) Compose(Notification n, string? frontendBaseUrl)
    {
        var body = n.Message;
        var link = BuildLink(n, frontendBaseUrl);
        if (link is not null)
            body = $"{n.Message}\n\nОткрыть в системе: {link}";
        return (n.Title, body);
    }

    internal static string? BuildLink(Notification n, string? frontendBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            return null;

        var root = frontendBaseUrl.TrimEnd('/');
        var path = n.RelatedEntityType switch
        {
            "Approval" => "/approval",
            "Invoice" => "/invoice",
            "Order" when n.RelatedEntityId is { } id => $"/orders/{id}",
            "Order" => "/orders",
            "GoodsReceipt" => "/warehouse",
            _ => null
        };
        return path is null ? root : root + path;
    }
}
