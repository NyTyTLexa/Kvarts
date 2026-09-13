using System.Security.Cryptography;
using System.Text;
using ProcurementSystem.Services.Retail.Clients;

namespace ProcurementSystem.Services.Retail.Retail;

public interface IRetailImportService
{
    Task<RetailImportResult> ImportAsync(IReadOnlyList<RetailHitDto> hits, CancellationToken ct = default);
}

/// <summary>
/// Запись найденных витрин в каталог по HTTP: магазин = Vendor, карточка = Product, цена = PriceListItem.
/// Свою копию номенклатуры не держим. Сопоставление — точный артикул, иначе точное имя через suggest.
/// </summary>
public sealed class RetailImportService(ICatalogClient catalog) : IRetailImportService
{
    public async Task<RetailImportResult> ImportAsync(IReadOnlyList<RetailHitDto> hits, CancellationToken ct = default)
    {
        if (hits.Count == 0) return new RetailImportResult(0, 0, 0, ["Нечего импортировать."]);

        var vendorsCreated = 0;
        var productsCreated = 0;
        var offers = 0;
        var errors = new List<string>();

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
                var vendor = await catalog.FindVendorByNameAsync(vendorName, ct);
                if (vendor is null)
                {
                    vendor = await catalog.CreateVendorAsync(vendorName, 2, ct);
                    vendorsCreated++;
                }

                var sku = string.IsNullOrWhiteSpace(hit.Sku) ? MakeSku(hit.Host, hit.Url) : hit.Sku.Trim();
                var product = await catalog.FindBySkuAsync(sku, ct);
                if (product is null)
                {
                    try { product = await catalog.SuggestByNameAsync(hit.Name.Trim(), ct); }
                    catch { /* каталог не ответил на suggest — создаём с нуля */ }
                }

                if (product is null)
                {
                    product = await catalog.CreateProductAsync(
                        sku,
                        hit.Name.Trim(),
                        string.IsNullOrWhiteSpace(hit.Brand) ? null : hit.Brand.Trim(),
                        "Розница",
                        ct);
                    productsCreated++;
                }

                var stock = hit.InStock ? 5 : 0;
                var lead = hit.InStock ? 0 : 5;
                var existing = await catalog.FindOfferAsync(product.Id, vendor.Id, ct);
                if (existing is null)
                {
                    var created = await catalog.CreateOfferAsync(product.Id, vendor.Id, hit.Price, lead, stock, ct);
                    if (created is null)
                    {
                        errors.Add($"{hit.Shop}: Catalog не принял оффер.");
                        continue;
                    }
                }
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
