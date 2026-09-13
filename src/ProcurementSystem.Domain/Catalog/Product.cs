using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Catalog;

/// <summary>Позиция номенклатуры (единица оборудования).</summary>
public class Product : AuditableEntity
{
    public string Sku { get; set; } = default!;            // артикул
    public string Name { get; set; } = default!;           // наименование
    public string? Manufacturer { get; set; }              // производитель
    public string? Category { get; set; }

    public ICollection<PriceListItem> Offers { get; set; } = new List<PriceListItem>();
}
