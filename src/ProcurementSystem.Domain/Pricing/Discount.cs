using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Pricing;

/// <summary>
/// Скидка по вендору или производителю, вводимая РП вручную (ТЗ UC-03, п.5.4).
/// Применяется автоматически при формировании КП. Имеет срок действия (с/по);
/// обе даты nullable — null/null означает «бессрочно».
/// Цель ровно одна: либо конкретный поставщик (VendorId), либо производитель (Manufacturer).
/// </summary>
public class Discount : AuditableEntity
{
    public Guid? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string? Manufacturer { get; set; }            // скидка по производителю (null, если по вендору)

    public decimal Percent { get; set; }                 // 0..100

    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }

    public string? CreatedBy { get; set; }                // пользователь, введший скидку (preferred_username)

    /// <summary>Активна ли скидка на момент времени (срок действия). Бессрочная — всегда активна.</summary>
    public bool IsActiveAt(DateTime utcNow) =>
        (ValidFromUtc is null || utcNow >= ValidFromUtc) &&
        (ValidToUtc is null || utcNow <= ValidToUtc);
}
