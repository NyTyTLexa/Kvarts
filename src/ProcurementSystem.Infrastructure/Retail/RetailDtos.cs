namespace ProcurementSystem.Infrastructure.Retail;

public record RetailHitDto(
    string Shop,
    string Host,
    string Name,
    string? Brand,
    string? Sku,
    decimal Price,
    string Currency,
    bool InStock,
    string Url,
    string Source);

public record RetailShopDto(
    string Host,
    string DisplayName,
    string Kind,
    DateTime? LastSuccessUtc,
    int HitCount,
    string? LastError);

public class RetailSearchRequest
{
    public string Q { get; set; } = "";
    public bool Wildberries { get; set; } = true;
    public bool Discover { get; set; } = true;
    public bool KnownShops { get; set; } = true;
    public string? Url { get; set; }
    public int Limit { get; set; } = 16;
}

public record RetailSearchResult(
    string Query,
    IReadOnlyList<RetailHitDto> Hits,
    IReadOnlyList<RetailShopDto> Shops,
    IReadOnlyList<string> Errors);

public class RetailImportRequest
{
    public IReadOnlyList<RetailHitDto> Hits { get; set; } = [];
}

public record RetailImportResult(
    int VendorsCreated,
    int ProductsCreated,
    int OffersUpserted,
    IReadOnlyList<string> Errors);
