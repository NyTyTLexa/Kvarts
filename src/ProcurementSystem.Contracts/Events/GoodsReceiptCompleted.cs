namespace ProcurementSystem.Contracts.Events;

/// <summary>
/// Приёмка проведена. Logistics кладёт в свой Outbox той же транзакцией, что и смену статуса,
/// публикует в NATS JetStream как <c>procurement.GoodsReceiptCompleted</c>.
/// Commercial по <see cref="SpecificationId"/> находит счёт «ОжиданиеПоставки»
/// и продвигает ЖЦ до «ПришёлНаСклад» → «ОтраженоВ1С».
/// </summary>
public record GoodsReceiptCompleted(
    Guid ReceiptId,
    Guid OrderId,
    Guid? SpecificationId,
    string OrderNumber,
    string? Customer,
    DateTime CompletedAtUtc,
    DateTime OccurredAtUtc,
    decimal ReceivedAmount,
    string? WarehouseRef,
    string? AccountingRef,
    IReadOnlyList<GoodsReceiptCompletedLine> Lines);

public record GoodsReceiptCompletedLine(
    Guid LineId,
    Guid? ProductId,
    string? Sku,
    string Name,
    int OrderedQty,
    int ReceivedQty,
    int Discrepancy,
    decimal UnitPrice);
