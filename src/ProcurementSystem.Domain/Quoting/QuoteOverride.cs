using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Quoting;

/// <summary>
/// Ручная корректировка выбора поставщика для позиции спецификации (ТЗ п.5.5):
/// фиксирует выбранного вручную поставщика, чтобы он применялся при генерации КП
/// вместо автоматической стратегии. Одна запись на позицию (уникальный индекс).
/// </summary>
public class QuoteOverride : AuditableEntity
{
    public Guid SpecificationItemId { get; set; }
    public SpecificationItem SpecificationItem { get; set; } = default!;

    public Guid VendorId { get; set; }
    public string? UpdatedBy { get; set; }      // кто внёс корректировку
}
