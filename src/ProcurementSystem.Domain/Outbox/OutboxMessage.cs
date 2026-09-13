namespace ProcurementSystem.Domain.Outbox;

/// <summary>
/// Запись Outbox. Пишется в ту же транзакцию, что и бизнес-данные, затем фоновый
/// процессор публикует её в NATS JetStream и проставляет ProcessedAtUtc.
/// Гарантирует, что событие не потеряется, даже если брокер недоступен в момент записи.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = default!;           // тип события (имя класса)
    public string Payload { get; set; } = default!;        // JSON тела события
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }          // null = ещё не отправлено
    public string? Error { get; set; }
}
