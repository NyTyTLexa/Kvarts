using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Catalog;

/// <summary>
/// Производитель оборудования (ТЗ п.5.2 — отдельный справочник, не просто строковое поле товара).
/// Product.Manufacturer остаётся строкой (денормализовано, как и раньше) — справочник даёт
/// РП/менеджеру справочников единое место ведения списка брендов со страной и статусом.
/// </summary>
public class Manufacturer : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
}
