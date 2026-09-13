namespace ProcurementSystem.Contracts.Events;

/// <summary>
/// Товар создан или обновлён в транзакционной БД. Кладётся в Outbox, публикуется в NATS,
/// Worker слушает и переиндексирует документ в поисковом движке.
/// </summary>
public record ProductUpserted(Guid ProductId, DateTime OccurredAtUtc);
