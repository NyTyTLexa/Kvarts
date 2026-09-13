using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Pricing;

/// <summary>
/// Точка истории цены: фиксируется при каждом создании/изменении оффера,
/// чтобы видеть динамику цены товара у поставщика во времени.
/// </summary>
public class PriceHistoryEntry : Entity
{
    public Guid ProductId { get; set; }
    public Guid VendorId { get; set; }
    public decimal Price { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
