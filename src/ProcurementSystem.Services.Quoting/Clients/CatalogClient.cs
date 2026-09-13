using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcurementSystem.Services.Quoting.Clients;

public interface ICatalogClient
{
    Task<int> CountProductsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogProductSnapshot>> ListAllProductsAsync(CancellationToken ct = default);
    Task<CatalogProductSnapshot?> GetProductAsync(Guid id, CancellationToken ct = default);
    Task<CatalogProductSnapshot?> FindBySkuAsync(string sku, CancellationToken ct = default);
    Task<bool> ProductExistsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, CatalogProductBundle>> GetBundlesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicableDiscount>> GetActiveDiscountsAsync(
        IReadOnlyCollection<Guid> vendorIds, IReadOnlyCollection<string> manufacturers, CancellationToken ct = default);
}

/// <summary>
/// Каталог — чужой сервис. Товары и офферы читаем с <c>/api/catalog/...</c>,
/// скидки — с <c>/api/discounts</c> того же хоста (отдельного HTTP GetActiveFor в Catalog нет).
/// </summary>
public sealed class CatalogClient(HttpClient http, ILogger<CatalogClient> log) : ICatalogClient
{
    const int BrowsePageSize = 48;
    const int MaxBrowsePages = 500;

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<int> CountProductsAsync(CancellationToken ct = default)
    {
        var page = await GetBrowsePageAsync(1, 1, ct);
        return page?.Total ?? 0;
    }

    public async Task<IReadOnlyList<CatalogProductSnapshot>> ListAllProductsAsync(CancellationToken ct = default)
    {
        var acc = new List<CatalogProductSnapshot>();
        var total = int.MaxValue;
        for (var page = 1; page <= MaxBrowsePages && acc.Count < total; page++)
        {
            var batch = await GetBrowsePageAsync(page, BrowsePageSize, ct);
            if (batch is null) break;
            total = batch.Total;
            var items = batch.Items ?? [];
            if (items.Count == 0) break;
            foreach (var card in items)
                acc.Add(ToSnapshot(card));
            if (items.Count < BrowsePageSize) break;
        }
        return acc;
    }

    public async Task<CatalogProductSnapshot?> GetProductAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await GetDetailAsync(id, ct);
        return detail is null ? null : ToSnapshot(detail.Card);
    }

    public async Task<CatalogProductSnapshot?> FindBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var q = sku.Trim();

        var suggest = await GetOrNullAsync<List<SuggestJson>>(
            $"api/catalog/suggest?q={Uri.EscapeDataString(q)}&limit=16", ct);
        var hit = suggest?.FirstOrDefault(s => string.Equals(s.Sku, q, StringComparison.OrdinalIgnoreCase));
        if (hit is not null)
            return new CatalogProductSnapshot(hit.Id, hit.Sku, hit.Name, hit.Manufacturer, hit.Category);

        var listed = await GetOrNullAsync<PagedJson<ProductJson>>(
            $"api/products?search={Uri.EscapeDataString(q)}&page=1&pageSize=50", ct);
        var product = listed?.Items?.FirstOrDefault(p => string.Equals(p.Sku, q, StringComparison.OrdinalIgnoreCase));
        return product is null
            ? null
            : new CatalogProductSnapshot(product.Id, product.Sku, product.Name, product.Manufacturer, product.Category);
    }

    public async Task<bool> ProductExistsAsync(Guid id, CancellationToken ct = default) =>
        await GetProductAsync(id, ct) is not null;

    public async Task<IReadOnlyDictionary<Guid, CatalogProductBundle>> GetBundlesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, CatalogProductBundle>();

        var result = new Dictionary<Guid, CatalogProductBundle>(ids.Count);
        using var gate = new SemaphoreSlim(8);
        var tasks = ids.Select(async id =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var detail = await GetDetailAsync(id, ct);
                if (detail is null) return;
                var last = new Dictionary<Guid, DateTime>();
                foreach (var h in detail.History ?? [])
                {
                    if (!last.TryGetValue(h.VendorId, out var at) || h.RecordedAtUtc > at)
                        last[h.VendorId] = h.RecordedAtUtc;
                }
                var bundle = new CatalogProductBundle(
                    ToSnapshot(detail.Card),
                    (detail.Offers ?? []).Select(o => new CatalogOfferSnapshot(
                        o.ProductId, o.VendorId, o.VendorName, o.Price, o.LeadTimeDays, o.StockQuantity)).ToList(),
                    last);
                lock (result) result[id] = bundle;
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
        return result;
    }

    public async Task<IReadOnlyList<ApplicableDiscount>> GetActiveDiscountsAsync(
        IReadOnlyCollection<Guid> vendorIds, IReadOnlyCollection<string> manufacturers, CancellationToken ct = default)
    {
        if (vendorIds.Count == 0 && manufacturers.Count == 0) return [];

        var rows = await GetOrNullAsync<List<DiscountJson>>("api/discounts", ct);
        if (rows is null || rows.Count == 0) return [];

        var vendors = vendorIds.ToHashSet();
        var mfrs = manufacturers.ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        return rows
            .Where(d => d.IsActive || IsActive(d, now))
            .Where(d => (d.VendorId is { } vid && vendors.Contains(vid))
                     || (d.Manufacturer is { Length: > 0 } m && mfrs.Contains(m)))
            .Select(d => new ApplicableDiscount(d.VendorId, d.Manufacturer, d.Percent))
            .ToList();
    }

    async Task<PagedJson<CardJson>?> GetBrowsePageAsync(int page, int pageSize, CancellationToken ct) =>
        await GetOrNullAsync<PagedJson<CardJson>>($"api/catalog?page={page}&pageSize={pageSize}", ct);

    async Task<DetailJson?> GetDetailAsync(Guid id, CancellationToken ct) =>
        await GetOrNullAsync<DetailJson>($"api/catalog/{id}", ct);

    async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log.LogWarning("Catalog {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
            response.EnsureSuccessStatusCode();
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    static CatalogProductSnapshot ToSnapshot(CardJson c) =>
        new(c.Id, c.Sku, c.Name, c.Manufacturer, c.Category);

    static bool IsActive(DiscountJson d, DateTime now) =>
        (d.ValidFromUtc is null || d.ValidFromUtc <= now) && (d.ValidToUtc is null || d.ValidToUtc >= now);

    static string Trim(string s) => s.Length <= 400 ? s : s[..400];

    sealed record PagedJson<T>(IReadOnlyList<T>? Items, int Total, int Page, int PageSize);
    sealed record CardJson(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
    sealed record ProductJson(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
    sealed record SuggestJson(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
    sealed record OfferJson(Guid Id, Guid ProductId, Guid VendorId, string VendorName, decimal Price, int LeadTimeDays, int StockQuantity);
    sealed record HistoryJson(DateTime RecordedAtUtc, decimal Price, Guid VendorId);
    sealed record DetailJson(CardJson Card, IReadOnlyList<OfferJson>? Offers, IReadOnlyList<HistoryJson>? History);
    sealed record DiscountJson(
        Guid Id, Guid? VendorId, string? Manufacturer, decimal Percent,
        DateTime? ValidFromUtc, DateTime? ValidToUtc, bool IsActive);
}
