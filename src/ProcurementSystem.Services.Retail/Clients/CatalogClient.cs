using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcurementSystem.Services.Retail.Clients;

public interface ICatalogClient
{
    Task<CatalogVendorDto?> FindVendorByNameAsync(string name, CancellationToken ct = default);
    Task<CatalogVendorDto> CreateVendorAsync(string name, int defaultLeadTimeDays, CancellationToken ct = default);
    Task<CatalogProductDto?> FindBySkuAsync(string sku, CancellationToken ct = default);
    Task<CatalogProductDto?> SuggestByNameAsync(string name, CancellationToken ct = default);
    Task<CatalogProductDto> CreateProductAsync(string sku, string name, string? manufacturer, string? category, CancellationToken ct = default);
    Task<CatalogOfferDto?> FindOfferAsync(Guid productId, Guid vendorId, CancellationToken ct = default);
    Task<CatalogOfferDto?> CreateOfferAsync(Guid productId, Guid vendorId, decimal price, int leadTimeDays, int stockQuantity, CancellationToken ct = default);
}

/// <summary>
/// Каталог — чужой сервис. Номенклатура, вендоры и офферы читаем и пишем по HTTP,
/// Bearer входящего запроса пробрасывает <see cref="AuthForwardHandler"/>.
/// </summary>
public sealed class CatalogClient(HttpClient http, ILogger<CatalogClient> log) : ICatalogClient
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<CatalogVendorDto?> FindVendorByNameAsync(string name, CancellationToken ct = default)
    {
        var q = (name ?? "").Trim();
        if (q.Length == 0) return null;
        var page = await GetOrNullAsync<PagedJson<VendorJson>>(
            $"api/vendors?search={Uri.EscapeDataString(q)}&page=1&pageSize=50", ct);
        var row = page?.Items?.FirstOrDefault(v => string.Equals(v.Name, q, StringComparison.OrdinalIgnoreCase));
        return row is null ? null : new CatalogVendorDto(row.Id, row.Name, row.Inn, row.DefaultLeadTimeDays);
    }

    public async Task<CatalogVendorDto> CreateVendorAsync(string name, int defaultLeadTimeDays, CancellationToken ct = default)
    {
        var created = await PostAsync<VendorJson>("api/vendors", new
        {
            name,
            inn = (string?)null,
            defaultLeadTimeDays
        }, ct);
        if (created is null) throw new InvalidOperationException("Catalog не вернул созданного поставщика.");
        return new CatalogVendorDto(created.Id, created.Name, created.Inn, created.DefaultLeadTimeDays);
    }

    public async Task<CatalogProductDto?> FindBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var q = sku.Trim();

        var suggest = await GetOrNullAsync<List<SuggestJson>>(
            $"api/catalog/suggest?q={Uri.EscapeDataString(q)}&limit=16", ct);
        var hit = suggest?.FirstOrDefault(s => string.Equals(s.Sku, q, StringComparison.OrdinalIgnoreCase));
        if (hit is not null)
            return new CatalogProductDto(hit.Id, hit.Sku, hit.Name, hit.Manufacturer, hit.Category);

        var listed = await GetOrNullAsync<PagedJson<ProductJson>>(
            $"api/products?search={Uri.EscapeDataString(q)}&page=1&pageSize=50", ct);
        var product = listed?.Items?.FirstOrDefault(p => string.Equals(p.Sku, q, StringComparison.OrdinalIgnoreCase));
        return product is null
            ? null
            : new CatalogProductDto(product.Id, product.Sku, product.Name, product.Manufacturer, product.Category);
    }

    public async Task<CatalogProductDto?> SuggestByNameAsync(string name, CancellationToken ct = default)
    {
        var q = (name ?? "").Trim();
        if (q.Length < 2) return null;

        var suggest = await GetOrNullAsync<List<SuggestJson>>(
            $"api/catalog/suggest?q={Uri.EscapeDataString(q)}&limit=16", ct);
        var exact = suggest?.FirstOrDefault(s => string.Equals(s.Name, q, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return new CatalogProductDto(exact.Id, exact.Sku, exact.Name, exact.Manufacturer, exact.Category);

        var listed = await GetOrNullAsync<PagedJson<ProductJson>>(
            $"api/products?search={Uri.EscapeDataString(q)}&page=1&pageSize=20", ct);
        var product = listed?.Items?.FirstOrDefault(p => string.Equals(p.Name, q, StringComparison.OrdinalIgnoreCase));
        return product is null
            ? null
            : new CatalogProductDto(product.Id, product.Sku, product.Name, product.Manufacturer, product.Category);
    }

    public async Task<CatalogProductDto> CreateProductAsync(
        string sku, string name, string? manufacturer, string? category, CancellationToken ct = default)
    {
        var created = await PostAsync<ProductJson>("api/products", new
        {
            sku,
            name,
            manufacturer,
            category
        }, ct);
        if (created is null) throw new InvalidOperationException("Catalog не вернул созданный товар.");
        return new CatalogProductDto(created.Id, created.Sku, created.Name, created.Manufacturer, created.Category);
    }

    public async Task<CatalogOfferDto?> FindOfferAsync(Guid productId, Guid vendorId, CancellationToken ct = default)
    {
        var offers = await GetOrNullAsync<List<OfferJson>>($"api/pricelist/by-product/{productId}", ct);
        var row = offers?.FirstOrDefault(o => o.VendorId == vendorId);
        return row is null
            ? null
            : new CatalogOfferDto(row.Id, row.ProductId, row.VendorId, row.VendorName,
                row.Price, row.Currency, row.LeadTimeDays, row.StockQuantity, row.SourceUrl);
    }

    public async Task<CatalogOfferDto?> CreateOfferAsync(
        Guid productId, Guid vendorId, decimal price, int leadTimeDays, int stockQuantity, CancellationToken ct = default)
    {
        var created = await PostAsync<OfferJson>("api/pricelist", new
        {
            productId,
            vendorId,
            price,
            leadTimeDays,
            stockQuantity
        }, ct);
        return created is null
            ? null
            : new CatalogOfferDto(created.Id, created.ProductId, created.VendorId, created.VendorName,
                created.Price, created.Currency, created.LeadTimeDays, created.StockQuantity, created.SourceUrl);
    }

    async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log.LogWarning("Catalog GET {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
            response.EnsureSuccessStatusCode();
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    async Task<T?> PostAsync<T>(string path, object payload, CancellationToken ct) where T : class
    {
        using var response = await http.PostAsJsonAsync(path, payload, Json, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log.LogWarning("Catalog POST {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
            response.EnsureSuccessStatusCode();
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    static string Trim(string s) => s.Length <= 400 ? s : s[..400];

    sealed record PagedJson<T>(IReadOnlyList<T>? Items, int Total, int Page, int PageSize);
    sealed record VendorJson(Guid Id, string Name, string? Inn, int DefaultLeadTimeDays);
    sealed record ProductJson(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
    sealed record SuggestJson(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
    sealed record OfferJson(
        Guid Id, Guid ProductId, Guid VendorId, string VendorName,
        decimal Price, string Currency, int LeadTimeDays, int StockQuantity, string? SourceUrl);
}
