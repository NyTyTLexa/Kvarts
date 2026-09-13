namespace ProcurementSystem.Domain.Integration;

/// <summary>Одна строка уведомления о приёмке во внешнюю складскую систему (WMS).</summary>
public record ReceiptNoticeLine(string Sku, string Name, int Quantity);

/// <summary>Уведомление о приёмке заказа на склад для внешней WMS.</summary>
public record ReceiptNotice(string OrderNumber, IReadOnlyList<ReceiptNoticeLine> Lines);

/// <summary>Результат регистрации приёмки во внешней складской системе.</summary>
public record WarehouseAckResult(bool Ok, string? ExternalRef, string? Error);

/// <summary>
/// Шлюз к внешней складской системе (WMS). За абстракцией — мок-адаптер (Этап 8);
/// реальная интеграция подключается новым адаптером с тем же интерфейсом.
/// </summary>
public interface IWarehouseGateway
{
    string SystemName { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Зарегистрировать приёмку заказа во внешней складской системе.</summary>
    Task<WarehouseAckResult> RegisterReceiptAsync(ReceiptNotice notice, CancellationToken ct = default);
}
