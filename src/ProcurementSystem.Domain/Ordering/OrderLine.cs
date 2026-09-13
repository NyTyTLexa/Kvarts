using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Ordering;

/// <summary>Строка заказа — снимок позиции КП (цена и поставщик зафиксированы).</summary>
public class OrderLine : Entity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid? ProductId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = default!;
    public int Quantity { get; set; }

    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public int LeadTimeDays { get; set; }
}
