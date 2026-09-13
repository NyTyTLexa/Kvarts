using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Services.Catalog.Common;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Catalog;

public interface ICatalogBrowseService
{
    Task<PagedResult<CatalogCardDto>> BrowseAsync(
        int page, int pageSize, string? search, string? category, string? manufacturer,
        Guid? vendorId, bool inStockOnly, string? sort, CancellationToken ct = default);

    Task<CatalogProductDetailDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogCardDto>> CompareAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogSuggestDto>> SuggestAsync(string? q, int limit = 8, CancellationToken ct = default);
}

/// <summary>
/// Витрина каталога: те же товары, но с мин/макс ценой, числом поставщиков и остатком.
/// Это B2B e-catalog поверх прайсов, без отдельной витрины.
/// </summary>
public class CatalogBrowseService(CatalogDbContext db) : ICatalogBrowseService
{
    public async Task<PagedResult<CatalogCardDto>> BrowseAsync(
        int page, int pageSize, string? search, string? category, string? manufacturer,
        Guid? vendorId, bool inStockOnly, string? sort, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);

        var q = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => EF.Functions.ILike(p.Name, $"%{search}%")
                          || EF.Functions.ILike(p.Sku, $"%{search}%")
                          || (p.Manufacturer != null && EF.Functions.ILike(p.Manufacturer, $"%{search}%")));
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(p => p.Category == category
                             || (p.Category != null && p.Category.StartsWith(category + " / ")));
        if (!string.IsNullOrWhiteSpace(manufacturer))
            q = q.Where(p => p.Manufacturer == manufacturer);
        if (vendorId is { } vid)
            q = q.Where(p => p.Offers.Any(o => o.VendorId == vid));
        if (inStockOnly)
            q = q.Where(p => p.Offers.Any(o => o.StockQuantity > 0));

        q = sort switch
        {
            "priceAsc" => q.OrderBy(p => p.Offers.Min(o => (decimal?)o.Price) ?? decimal.MaxValue).ThenBy(p => p.Name),
            "priceDesc" => q.OrderByDescending(p => p.Offers.Max(o => (decimal?)o.Price) ?? 0).ThenBy(p => p.Name),
            "offers" => q.OrderByDescending(p => p.Offers.Count()).ThenBy(p => p.Name),
            "stock" => q.OrderByDescending(p => p.Offers.Sum(o => o.StockQuantity)).ThenBy(p => p.Name),
            _ => q.OrderBy(p => p.Name)
        };

        var total = await q.CountAsync(ct);
        var items = await ProjectCards(q.Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<CatalogCardDto>(items, total, page, pageSize);
    }

    public async Task<CatalogProductDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var card = await ProjectCards(db.Products.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct);
        if (card is null) return null;

        var offers = await db.PriceListItems.AsNoTracking()
            .Where(o => o.ProductId == id)
            .OrderBy(o => o.Price)
            .Select(o => new OfferDto(o.Id, o.ProductId, o.VendorId, o.Vendor.Name,
                o.Price, o.Currency, o.LeadTimeDays, o.StockQuantity, o.SourceUrl))
            .ToListAsync(ct);

        IReadOnlyList<CatalogCardDto> analogs = [];
        if (!string.IsNullOrWhiteSpace(card.Category))
        {
            var category = card.Category;
            var manufacturer = card.Manufacturer;
            analogs = await ProjectCards(
                    db.Products.AsNoTracking()
                        .Where(x => x.Category == category && x.Id != id)
                        .OrderBy(x => x.Manufacturer == manufacturer ? 0 : 1)
                        .ThenBy(x => x.Name)
                        .Take(12))
                .ToListAsync(ct);
        }

        var history = await db.PriceHistory.AsNoTracking()
            .Where(h => h.ProductId == id)
            .OrderByDescending(h => h.RecordedAtUtc)
            .Take(60)
            .Select(h => new CatalogPricePointDto(h.RecordedAtUtc, h.Price, h.VendorId))
            .ToListAsync(ct);
        history.Reverse();

        return new CatalogProductDetailDto(card, offers, analogs, history);
    }

    public async Task<IReadOnlyList<CatalogCardDto>> CompareAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var unique = ids.Distinct().Take(48).ToList();
        if (unique.Count == 0) return [];

        var found = await ProjectCards(db.Products.AsNoTracking().Where(p => unique.Contains(p.Id))).ToListAsync(ct);
        var map = found.ToDictionary(c => c.Id);
        return unique.Where(map.ContainsKey).Select(id => map[id]).ToList();
    }

    public async Task<IReadOnlyList<CatalogSuggestDto>> SuggestAsync(string? q, int limit = 8, CancellationToken ct = default)
    {
        q = q?.Trim();
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return [];
        limit = Math.Clamp(limit, 1, 16);
        var like = $"%{q}%";

        var rows = await db.Products.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, like)
                     || EF.Functions.ILike(p.Sku, like)
                     || (p.Manufacturer != null && EF.Functions.ILike(p.Manufacturer, like)))
            .Select(p => new CatalogSuggestDto(
                p.Id, p.Sku, p.Name, p.Manufacturer,
                p.Offers.Min(o => (decimal?)o.Price),
                p.Category))
            .Take(32)
            .ToListAsync(ct);
        var needle = q.ToLowerInvariant();
        return rows
            .OrderBy(p =>
                p.Sku.Equals(q, StringComparison.OrdinalIgnoreCase) ? 0
                : p.Sku.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 1
                : p.Name.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 2
                : 3)
            .ThenBy(p => p.Name)
            .Take(limit)
            .ToList();
    }

    private static IQueryable<CatalogCardDto> ProjectCards(IQueryable<Product> q) =>
        q.Select(p => new CatalogCardDto(
            p.Id, p.Sku, p.Name, p.Manufacturer, p.Category,
            p.Offers.Min(o => (decimal?)o.Price),
            p.Offers.Max(o => (decimal?)o.Price),
            p.Offers.Count(),
            p.Offers.Sum(o => o.StockQuantity),
            p.Offers.OrderBy(o => o.Price).Select(o => o.Vendor.Name).FirstOrDefault(),
            p.Offers.Min(o => (int?)o.LeadTimeDays)));
}
