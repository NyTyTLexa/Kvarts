namespace ProcurementSystem.Services.Retail.Clients;

public sealed record CatalogVendorDto(Guid Id, string Name, string? Inn, int DefaultLeadTimeDays);

public sealed record CatalogProductDto(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);

public sealed record CatalogOfferDto(
    Guid Id, Guid ProductId, Guid VendorId, string VendorName,
    decimal Price, string Currency, int LeadTimeDays, int StockQuantity, string? SourceUrl);
