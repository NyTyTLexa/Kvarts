namespace ProcurementSystem.Services.Quoting.Clients;

/// <summary>Снимок товара каталога. Нужные для отображения поля лежат здесь, не через Include.</summary>
public sealed record CatalogProductSnapshot(
    Guid Id,
    string Sku,
    string Name,
    string? Manufacturer,
    string? Category);

public sealed record CatalogOfferSnapshot(
    Guid ProductId,
    Guid VendorId,
    string VendorName,
    decimal Price,
    int LeadTimeDays,
    int StockQuantity);

/// <summary>Товар + офферы + последняя дата цены по вендору — то, что генератору КП нужно с витрины.</summary>
public sealed record CatalogProductBundle(
    CatalogProductSnapshot Product,
    IReadOnlyList<CatalogOfferSnapshot> Offers,
    IReadOnlyDictionary<Guid, DateTime> LastPriceAtByVendor);

/// <summary>Лёгкая проекция активной скидки для генератора КП (как в монолите).</summary>
public sealed record ApplicableDiscount(Guid? VendorId, string? Manufacturer, decimal Percent);
