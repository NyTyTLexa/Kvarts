using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcurementSystem.Infrastructure.Audit;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Api.BackgroundServices;

/// <summary>
/// Периодически удаляет записи журнала аудита старее AuditOptions.RetentionDays,
/// чтобы таблица не росла бесконечно (ТЗ: ретенция ~3 года). Сбои очистки не влияют
/// на работу API. Удаление пакетное (ExecuteDeleteAsync) — без загрузки сущностей в память.
/// </summary>
public class AuditRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditOptions> opts,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, opts.Value.CleanupIntervalHours));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow - TimeSpan.FromDays(Math.Max(1, opts.Value.RetentionDays));

                var deleted = await db.AuditEntries
                    .Where(a => a.OccurredAtUtc < cutoff)
                    .ExecuteDeleteAsync(ct);

                if (deleted > 0)
                    logger.LogInformation(
                        "Аудит: удалено {Count} записей старше {Days} дн.", deleted, opts.Value.RetentionDays);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Ошибка очистки журнала аудита"); }

            await Task.Delay(interval, ct);
        }
    }
}
