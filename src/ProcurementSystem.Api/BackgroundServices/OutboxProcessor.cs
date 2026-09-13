using System.Text;
using Microsoft.EntityFrameworkCore;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using ProcurementSystem.Contracts;
using ProcurementSystem.Domain.Messaging;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Api.BackgroundServices;

/// <summary>
/// Раз в секунду выбирает необработанные записи Outbox, публикует их в NATS JetStream
/// и проставляет ProcessedAtUtc. Так гарантируется доставка событий в поисковый индекс
/// даже при кратковременной недоступности брокера.
/// </summary>
public class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    INatsJSContext js,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await EnsureStreamAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Ошибка обработки Outbox"); }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    private async Task EnsureStreamAsync(CancellationToken ct)
    {
        for (int attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await js.CreateOrUpdateStreamAsync(
                    new StreamConfig(Messaging.Stream, [Messaging.SubjectWildcard]), ct);
                logger.LogInformation("JetStream-стрим '{Stream}' готов", Messaging.Stream);
                return;
            }
            catch (Exception ex) when (attempt <= 15)
            {
                logger.LogWarning("NATS недоступен (попытка {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(200)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        foreach (var m in batch)
        {
            await bus.PublishAsync(Messaging.Subject(m.Type), Encoding.UTF8.GetBytes(m.Payload), ct);
            m.ProcessedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Outbox: опубликовано {Count} событий", batch.Count);
    }
}
