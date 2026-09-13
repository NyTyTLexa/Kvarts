using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Catalog;

/// <summary>Поставщик оборудования.</summary>
public class Vendor : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Inn { get; set; }                       // ИНН
    public int DefaultLeadTimeDays { get; set; }           // срок поставки по умолчанию

    public ICollection<PriceListItem> PriceListItems { get; set; } = new List<PriceListItem>();
}
