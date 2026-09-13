using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Infrastructure.Catalog;

namespace ProcurementSystem.Tests;

public class CatalogBrowseTests
{
    [Fact]
    public async Task Browse_exposes_min_price_and_offer_count()
    {
        using var db = TestDb.Create();
        var v1 = new Vendor { Name = "Дешёвый", DefaultLeadTimeDays = 10 };
        var v2 = new Vendor { Name = "Дорогой", DefaultLeadTimeDays = 3 };
        var p = new Product { Sku = "CAT-1", Name = "Коммутатор тестовый", Manufacturer = "Cisco", Category = "Сеть" };
        db.Vendors.AddRange(v1, v2);
        db.Products.Add(p);
        db.PriceListItems.AddRange(
            new PriceListItem { Product = p, Vendor = v1, Price = 100m, LeadTimeDays = 10, StockQuantity = 2 },
            new PriceListItem { Product = p, Vendor = v2, Price = 180m, LeadTimeDays = 3, StockQuantity = 5 });
        await db.SaveChangesAsync();

        var page = await new CatalogBrowseService(db).BrowseAsync(1, 24, null, null, null, null, false, "priceAsc");

        var card = Assert.Single(page.Items);
        Assert.Equal(100m, card.MinPrice);
        Assert.Equal(180m, card.MaxPrice);
        Assert.Equal(2, card.OfferCount);
        Assert.Equal(7, card.TotalStock);
        Assert.Equal("Дешёвый", card.CheapestVendor);
        Assert.Equal(3, card.MinLeadTimeDays);
    }

    [Fact]
    public async Task Get_returns_offers_analogs_and_history()
    {
        using var db = TestDb.Create();
        var v = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var a = new Product { Sku = "SW-1", Name = "Catalyst 9300", Manufacturer = "Cisco", Category = "Сеть / Коммутаторы" };
        var b = new Product { Sku = "SW-2", Name = "Catalyst 9200", Manufacturer = "Cisco", Category = "Сеть / Коммутаторы" };
        db.Vendors.Add(v);
        db.Products.AddRange(a, b);
        db.PriceListItems.Add(new PriceListItem { Product = a, Vendor = v, Price = 250_000m, LeadTimeDays = 14, StockQuantity = 3 });
        db.PriceHistory.Add(new PriceHistoryEntry { ProductId = a.Id, VendorId = v.Id, Price = 260_000m, LeadTimeDays = 14, RecordedAtUtc = DateTime.UtcNow.AddDays(-10) });
        db.PriceHistory.Add(new PriceHistoryEntry { ProductId = a.Id, VendorId = v.Id, Price = 250_000m, LeadTimeDays = 14, RecordedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var detail = await new CatalogBrowseService(db).GetAsync(a.Id);

        Assert.NotNull(detail);
        Assert.Equal("SW-1", detail.Card.Sku);
        Assert.Equal(250_000m, detail.Card.MinPrice);
        Assert.Single(detail.Offers);
        Assert.Equal("Поставщик", detail.Offers[0].VendorName);
        var analog = Assert.Single(detail.Analogs);
        Assert.Equal("SW-2", analog.Sku);
        Assert.Equal(2, detail.History.Count);
        Assert.Equal(250_000m, detail.History[^1].Price);
    }

    [Fact]
    public async Task Compare_preserves_request_order_and_skips_unknown()
    {
        using var db = TestDb.Create();
        var p1 = new Product { Sku = "A", Name = "Alpha" };
        var p2 = new Product { Sku = "B", Name = "Beta" };
        db.Products.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var missing = Guid.NewGuid();
        var cards = await new CatalogBrowseService(db).CompareAsync([p2.Id, missing, p1.Id]);

        Assert.Equal(2, cards.Count);
        Assert.Equal("B", cards[0].Sku);
        Assert.Equal("A", cards[1].Sku);
    }

    [Fact]
    public async Task Browse_filters_by_vendor_and_in_stock()
    {
        using var db = TestDb.Create();
        var v1 = new Vendor { Name = "V1", DefaultLeadTimeDays = 5 };
        var v2 = new Vendor { Name = "V2", DefaultLeadTimeDays = 5 };
        var p1 = new Product { Sku = "P1", Name = "One" };
        var p2 = new Product { Sku = "P2", Name = "Two" };
        var p3 = new Product { Sku = "P3", Name = "Three" };
        db.Vendors.AddRange(v1, v2);
        db.Products.AddRange(p1, p2, p3);
        db.PriceListItems.AddRange(
            new PriceListItem { Product = p1, Vendor = v1, Price = 10m, StockQuantity = 0, LeadTimeDays = 7 },
            new PriceListItem { Product = p2, Vendor = v1, Price = 20m, StockQuantity = 4, LeadTimeDays = 7 },
            new PriceListItem { Product = p3, Vendor = v2, Price = 30m, StockQuantity = 9, LeadTimeDays = 2 });
        await db.SaveChangesAsync();

        var svc = new CatalogBrowseService(db);
        var byVendor = await svc.BrowseAsync(1, 24, null, null, null, v1.Id, false, "name");
        Assert.Equal(2, byVendor.Total);
        Assert.All(byVendor.Items, c => Assert.Contains(c.Sku, new[] { "P1", "P2" }));

        var inStock = await svc.BrowseAsync(1, 24, null, null, null, v1.Id, true, "stock");
        var card = Assert.Single(inStock.Items);
        Assert.Equal("P2", card.Sku);
        Assert.Equal(4, card.TotalStock);
    }

    [Fact]
    public async Task Browse_vendor_area_does_not_steal_similar_prefix()
    {
        using var db = TestDb.Create();
        db.Products.AddRange(
            new Product { Sku = "A", Name = "A", Category = "EKF / Модульное" },
            new Product { Sku = "B", Name = "B", Category = "EKF2 / Другое" });
        await db.SaveChangesAsync();

        var page = await new CatalogBrowseService(db).BrowseAsync(1, 24, null, "EKF", null, null, false, "name");
        var card = Assert.Single(page.Items);
        Assert.Equal("A", card.Sku);
    }

    [Fact]
    public async Task Get_unknown_product_returns_null()
    {
        using var db = TestDb.Create();
        Assert.Null(await new CatalogBrowseService(db).GetAsync(Guid.NewGuid()));
    }
}
