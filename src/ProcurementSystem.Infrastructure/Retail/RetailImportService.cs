using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Infrastructure.Outbox;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using System.Security.Cryptography;
using System.Text;

namespace ProcurementSystem.Infrastructure.Retail;

public interface IRetailImportService
{
    Task<RetailImportResult> ImportAsync(IReadOnlyList<RetailHitDto> hits, CancellationToken ct = default);
}

/// <summary>
/// Запись найденных витрин в тот же каталог: магазин = Vendor, карточка = Product, цена = PriceListItem.
/// Сопоставление с уже существующей номенклатурой — по артикулу, иначе ML по имени.
/// </summary>
public sealed class RetailImportService(AppDbContext db, IRelevanceMatcher matcher) : IRetailImportService
{
    public async Task<RetailImportResult> ImportAsync(IReadOnlyList<RetailHitDto> hits, CancellationToken ct = default)
    {
        if (hits.Count == 0) return new RetailImportResult(0, 0, 0, ["Нечего импортировать."]);

        var vendorsCreated = 0;
        var productsCreated = 0;
        var offers = 0;
        var errors = new List<string>();

        try { await matcher.EnsureReadyAsync(ct); }
        catch { /* пустой каталог — создаём товары с нуля */ }

        foreach (var hit in hits)
        {
            try
            {
                if (hit.Price <= 0 || string.IsNullOrWhiteSpace(hit.Name))
                {
                    errors.Add("Пропуск: нет имени или цены.");
                    continue;
                }

                var vendorName = string.IsNullOrWhiteSpace(hit.Shop) ? ShopDirectory.DisplayName(hit.Host) : hit.Shop;
                var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Name == vendorName, ct);
                if (vendor is null)
                {
                    vendor = new Vendor { Name = vendorName, DefaultLeadTimeDays = 2 };
                    db.Vendors.Add(vendor);
                    vendorsCreated++;
                    await db.SaveChangesAsync(ct);
                }

                var sku = string.IsNullOrWhiteSpace(hit.Sku) ? MakeSku(hit.Host, hit.Url) : hit.Sku.Trim();
                var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);
                if (product is null)
                {
                    Guid? matchedId = null;
                    try
                    {
                        var sug = await matcher.SuggestAsync(sku, hit.Name, 1, ct);
                        var top = sug.FirstOrDefault();
                        if (top is not null && top.Probability >= 0.55 && top.Kind != MatchKind.None)
                            matchedId = top.ProductId;
                    }
                    catch { /* matcher не готов */ }

                    if (matchedId is Guid id)
                        product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
                }

                if (product is null)
                {
                    product = new Product
                    {
                        Sku = sku,
                        Name = hit.Name.Trim(),
                        Manufacturer = string.IsNullOrWhiteSpace(hit.Brand) ? null : hit.Brand.Trim(),
                        Category = "Розница",
                    };
                    db.Products.Add(product);
                    productsCreated++;
                    db.EnqueueEvent(new ProductUpserted(product.Id, DateTime.UtcNow));
                    await db.SaveChangesAsync(ct);
                }

                var stock = hit.InStock ? 5 : 0;
                var lead = hit.InStock ? 0 : 5;
                var offer = await db.PriceListItems.FirstOrDefaultAsync(
                    o => o.ProductId == product.Id && o.VendorId == vendor.Id, ct);
                if (offer is null)
                {
                    offer = new PriceListItem
                    {
                        ProductId = product.Id,
                        VendorId = vendor.Id,
                        Price = hit.Price,
                        Currency = string.IsNullOrWhiteSpace(hit.Currency) ? "RUB" : hit.Currency,
                        LeadTimeDays = lead,
                        StockQuantity = stock,
                        SourceUrl = hit.Url,
                    };
                    db.PriceListItems.Add(offer);
                }
                else
                {
                    offer.Price = hit.Price;
                    offer.LeadTimeDays = lead;
                    offer.StockQuantity = stock;
                    offer.SourceUrl = hit.Url;
                    offer.UpdatedAtUtc = DateTime.UtcNow;
                }
                db.RecordPriceChange(product.Id, vendor.Id, hit.Price, lead);
                db.EnqueueEvent(new ProductUpserted(product.Id, DateTime.UtcNow));
                await db.SaveChangesAsync(ct);
                offers++;
            }
            catch (Exception ex)
            {
                errors.Add($"{hit.Shop}: {ex.Message}");
            }
        }

        return new RetailImportResult(vendorsCreated, productsCreated, offers, errors);
    }

    static string MakeSku(string host, string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var hex = Convert.ToHexString(bytes.AsSpan(0, 5));
        var h = ShopDirectory.NormalizeHost(host).Split('.')[0].ToUpperInvariant();
        if (h.Length > 8) h = h[..8];
        return $"WEB-{h}-{hex}";
    }
}
