using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using ProcurementSystem.Contracts;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Services.Commercial.Commercial;
using ProcurementSystem.Services.Commercial.Observability;

namespace ProcurementSystem.Services.Commercial.BackgroundServices;

/// <summary>
/// Durable-консьюмер JetStream: subject <c>procurement.GoodsReceiptCompleted</c>,
/// durable-имя <c>commercial-goods-receipt</c>. Успех — ack, ошибка обработки — nak
/// (сообщение вернётся в стрим, как у Worker на ProductUpserted).
/// </summary>
public class GoodsReceiptCompletedConsumer(
    ILogger<GoodsReceiptCompletedConsumer> logger,
    INatsJSContext js,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    public const string DurableName = "commercial-goods-receipt";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await EnsureStreamAsync(ct);

        var subject = Messaging.Subject(nameof(GoodsReceiptCompleted));
        var consumer = await js.CreateOrUpdateConsumerAsync(
            Messaging.Stream,
            new ConsumerConfig(DurableName) { FilterSubject = subject },
            ct);

        logger.LogInformation("Commercial слушает {Subject}, durable {Durable}", subject, DurableName);

        await foreach (var msg in consumer.ConsumeAsync<byte[]>(
            serializer: NatsRawSerializer<byte[]>.Default, cancellationToken: ct))
        {
            try
            {
                var evt = msg.Data is null ? null : JsonSerializer.Deserialize<GoodsReceiptCompleted>(msg.Data);
                if (evt is not null)
                {
                    using var activity = Telemetry.Source.StartActivity("invoice.advance-from-receipt");
                    activity?.SetTag("receipt.id", evt.ReceiptId.ToString());
                    activity?.SetTag("order.id", evt.OrderId.ToString());

                    using var scope = scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<GoodsReceiptCompletedHandler>();
                    await handler.HandleAsync(evt, ct);
                }
                await msg.AckAsync(cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки GoodsReceiptCompleted");
                await msg.NakAsync(cancellationToken: ct);
            }
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
                return;
            }
            catch (Exception ex) when (attempt <= 15)
            {
                logger.LogWarning("NATS недоступен (попытка {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
