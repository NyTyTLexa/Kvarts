namespace ProcurementSystem.Services.Catalog.Catalog;

public record VendorDto(Guid Id, string Name, string? Inn, int DefaultLeadTimeDays);
public record CreateVendorRequest(string Name, string? Inn, int DefaultLeadTimeDays);
public record UpdateVendorRequest(string Name, string? Inn, int DefaultLeadTimeDays);

public record ProductDto(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
public record CategoryDto(string Path, int ProductsCount);
public record CreateProductRequest(string Sku, string Name, string? Manufacturer, string? Category);
public record UpdateProductRequest(string Sku, string Name, string? Manufacturer, string? Category);

public record OfferDto(
    Guid Id, Guid ProductId, Guid VendorId, string VendorName,
    decimal Price, string Currency, int LeadTimeDays, int StockQuantity,
    string? SourceUrl = null);

public record CreateOfferRequest(Guid ProductId, Guid VendorId, decimal Price, int LeadTimeDays, int StockQuantity);

/// <summary>Карточка электронного каталога: номенклатура + сводка офферов.</summary>
public record CatalogCardDto(
    Guid Id,
    string Sku,
    string Name,
    string? Manufacturer,
    string? Category,
    decimal? MinPrice,
    decimal? MaxPrice,
    int OfferCount,
    int TotalStock,
    string? CheapestVendor,
    int? MinLeadTimeDays);

public record CatalogSuggestDto(
    Guid Id,
    string Sku,
    string Name,
    string? Manufacturer,
    decimal? MinPrice,
    string? Category = null);

public record CatalogPricePointDto(DateTime RecordedAtUtc, decimal Price, Guid VendorId);

public record CatalogProductDetailDto(
    CatalogCardDto Card,
    IReadOnlyList<OfferDto> Offers,
    IReadOnlyList<CatalogCardDto> Analogs,
    IReadOnlyList<CatalogPricePointDto> History);
