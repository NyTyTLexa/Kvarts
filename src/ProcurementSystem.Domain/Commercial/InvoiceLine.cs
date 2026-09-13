using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Commercial;

/// <summary>
/// Строка счёта NOC — снимок позиции согласованного варианта КП (ТЗ п.6.1:
/// «Позиция/Артикул/Количество» на уровне строки). Фиксируется при создании счёта:
/// закупочная цена со скидкой и цена клиенту с наценкой согласования.
/// </summary>
public class InvoiceLine : Entity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;

    public Guid? ProductId { get; set; }
    public string? Sku { get; set; }                 // Артикул
    public string Name { get; set; } = default!;     // Позиция
    public int Quantity { get; set; }                // Количество

    public string? VendorName { get; set; }          // поставщик (снимок)
    public string? Manufacturer { get; set; }        // производитель (снимок)

    public decimal UnitCost { get; set; }            // закупочная за ед. со скидкой, без НДС
    public decimal UnitPrice { get; set; }           // цена клиенту за ед. (с наценкой согласования)
    public decimal LineTotal { get; set; }           // UnitPrice × Quantity
}
