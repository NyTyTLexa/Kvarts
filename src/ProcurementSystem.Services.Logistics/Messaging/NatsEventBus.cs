using NATS.Client.Core;
using NATS.Client.JetStream;
using ProcurementSystem.Domain.Messaging;

namespace ProcurementSystem.Services.Logistics.Messaging;

/// <summary>
/// Публикация в NATS JetStream сырых байтов по заданному subject.
/// Гарантию доставки даёт JetStream (сообщение оседает в стриме до ack консьюмера).
/// </summary>
public class NatsEventBus(INatsJSContext jetStream) : IEventBus
{
    public async Task PublishAsync(string subject, byte[] payload, CancellationToken ct = default)
    {
        await jetStream.PublishAsync(
            subject,
            payload,
            serializer: NatsRawSerializer<byte[]>.Default,
            cancellationToken: ct);
    }
}
