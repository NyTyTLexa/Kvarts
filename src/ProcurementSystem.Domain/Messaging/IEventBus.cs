namespace ProcurementSystem.Domain.Messaging;

/// <summary>
/// Абстракция шины событий. Реализация — NATS JetStream.
/// Публикует уже сериализованное тело по заданному subject — этим пользуется процессор Outbox,
/// поэтому шина не зависит от конкретных типов событий.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(string subject, byte[] payload, CancellationToken ct = default);
}
