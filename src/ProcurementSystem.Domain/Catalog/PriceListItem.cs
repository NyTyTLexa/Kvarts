using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Catalog;

/// <summary>Оффер: конкретный товар у конкретного поставщика с ценой и сроком.</summary>
public class PriceListItem : AuditableEntity
{
    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public decimal Price { get; set; }
    public string Currency { get; set; } = "RUB";
    public int LeadTimeDays { get; set; }                  // срок поставки этого оффера
    public int StockQuantity { get; set; }                 // наличие на складе
    /// <summary>Ссылка на карточку витрины, если оффер снят с сайта, а не из Excel.</summary>
    public string? SourceUrl { get; set; }
}
