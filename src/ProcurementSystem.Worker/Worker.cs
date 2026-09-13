using System.Text.Json;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using ProcurementSystem.Contracts;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Infrastructure.Observability;
using ProcurementSystem.Infrastructure.Search;

namespace ProcurementSystem.Worker;

/// <summary>
/// Indexer worker: durable-консьюмер NATS JetStream. Слушает события procurement.ProductUpserted,
/// на каждое — переиндексирует товар в поисковом движке через IProductIndexer.
/// </summary>
public class Worker(
    ILogger<Worker> logger,
    INatsJSContext js,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await EnsureStreamAsync(ct);

        var subject = Messaging.Subject(nameof(ProductUpserted));
        var consumer = await js.CreateOrUpdateConsumerAsync(
            Messaging.Stream,
            new ConsumerConfig("indexer") { FilterSubject = subject },
            ct);

        logger.LogInformation("Indexer worker запущен, слушаю {Subject}", subject);

        await foreach (var msg in consumer.ConsumeAsync<byte[]>(
            serializer: NatsRawSerializer<byte[]>.Default, cancellationToken: ct))
        {
            try
            {
                var evt = msg.Data is null ? null : JsonSerializer.Deserialize<ProductUpserted>(msg.Data);
                if (evt is not null)
                {
                    using var activity = Telemetry.Source.StartActivity("index.product");
                    activity?.SetTag("product.id", evt.ProductId.ToString());

                    using var scope = scopeFactory.CreateScope();
                    var indexer = scope.ServiceProvider.GetRequiredService<IProductIndexer>();
                    await indexer.IndexAsync(evt.ProductId, ct);
                }
                await msg.AckAsync(cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки события индексации");
                await msg.NakAsync(cancellationToken: ct);   // вернуть в очередь на повтор
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
