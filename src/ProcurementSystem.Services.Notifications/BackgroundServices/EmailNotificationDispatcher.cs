using Microsoft.Extensions.Options;
using ProcurementSystem.Services.Notifications.Email;

namespace ProcurementSystem.Services.Notifications.BackgroundServices;

/// <summary>
/// Периодически рассылает email по новым системным уведомлениям (SMTP / MailHog).
/// Генерацию событий не вшивает в Commercial/Ordering/Logistics — переиспользует
/// INotificationService.SyncAsync и помечает Notification.EmailedAtUtc.
/// </summary>
public sealed class EmailNotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> opts,
    ILogger<EmailNotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, opts.Value.PollIntervalSeconds));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EmailNotificationProcessor>();
                await processor.DispatchOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Ошибка рассылки email-уведомлений"); }

            await Task.Delay(interval, ct);
        }
    }
}
