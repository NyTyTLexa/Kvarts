using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Logistics;

/// <summary>Статус приёмки заказа на склад.</summary>
public enum ReceiptStatus
{
    Черновик,   // создана из заказа, идёт сверка количества
    Проведено,  // завершена и выгружена во внешние системы (WMS/1С)
    Отменена
}

/// <summary>
/// Приёмка заказа на склад (ТЗ п.B11): по строкам сверяется заказанное и фактически принятое
/// количество, фиксируются расхождения. При проведении — выгрузка во внешние системы (WMS/1С, Этап 8).
/// </summary>
public class GoodsReceipt : AuditableEntity
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = default!;    // снимок номера заказа
    public string? Customer { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Черновик;
    public string? CreatedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? WarehouseRef { get; set; }              // ссылка из внешней WMS
    public string? AccountingRef { get; set; }             // номер документа в 1С

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();

    // ── Поведение приёмки (rich domain model) ─────────────────────────────────────────

    /// <summary>Черновик — единственное состояние, в котором приёмку можно менять и проводить.</summary>
    public bool IsDraft => Status == ReceiptStatus.Черновик;

    /// <summary>Сумма документа для 1С — по фактически принятому количеству.</summary>
    public decimal ReceivedAmount => Lines.Sum(l => l.ReceivedQty * l.UnitPrice);

    /// <summary>Скорректировать принятое количество по строке (не ниже нуля).</summary>
    public void SetReceived(GoodsReceiptLine line, int receivedQty)
    {
        line.ReceivedQty = Math.Max(0, receivedQty);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Провести приёмку: зафиксировать ссылки внешних систем и закрыть документ.</summary>
    public void Complete(string? warehouseRef, string? accountingRef)
    {
        WarehouseRef = warehouseRef;
        AccountingRef = accountingRef;
        Status = ReceiptStatus.Проведено;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>Строка приёмки: заказано vs принято. Расхождение вычисляется.</summary>
public class GoodsReceiptLine : Entity
{
    public Guid GoodsReceiptId { get; set; }
    public GoodsReceipt Receipt { get; set; } = default!;

    public Guid? ProductId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = default!;
    public int OrderedQty { get; set; }
    public int ReceivedQty { get; set; }
    public decimal UnitPrice { get; set; }                 // снимок цены из заказа (для суммы документа 1С)

    /// <summary>Расхождение: &lt;0 — недопоставка, &gt;0 — излишек, 0 — сходится.</summary>
    public int Discrepancy => ReceivedQty - OrderedQty;
}
