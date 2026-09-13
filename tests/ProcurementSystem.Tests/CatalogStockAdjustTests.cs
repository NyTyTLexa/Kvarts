using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Services.Catalog.Catalog;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Tests;

/// <summary>Резерв/возврат остатка в вынесенном Catalog — контракт POST /api/catalog/stock/adjust.</summary>
public class CatalogStockAdjustTests
{
    [Fact]
    public async Task Reserve_decreases_stock()
    {
        using var db = CreateDb();
        var offer = await SeedOfferAsync(db, stock: 10);

        await new StockAdjustService(db).AdjustAsync(
            [new StockAdjustItem(offer.ProductId, offer.VendorId, Quantity: 3, Sign: -1)]);

        Assert.Equal(7, (await db.PriceListItems.FindAsync(offer.Id))!.StockQuantity);
    }

    [Fact]
    public async Task Return_increases_stock()
    {
        using var db = CreateDb();
        var offer = await SeedOfferAsync(db, stock: 7);

        await new StockAdjustService(db).AdjustAsync(
            [new StockAdjustItem(offer.ProductId, offer.VendorId, Quantity: 3, Sign: +1)]);

        Assert.Equal(10, (await db.PriceListItems.FindAsync(offer.Id))!.StockQuantity);
    }

    [Fact]
    public async Task Reservation_never_goes_below_zero()
    {
        using var db = CreateDb();
        var offer = await SeedOfferAsync(db, stock: 2);

        await new StockAdjustService(db).AdjustAsync(
            [new StockAdjustItem(offer.ProductId, offer.VendorId, Quantity: 5, Sign: -1)]);

        Assert.Equal(0, (await db.PriceListItems.FindAsync(offer.Id))!.StockQuantity);
    }

    [Fact]
    public async Task Unknown_offer_is_skipped_and_does_not_fail_the_batch()
    {
        using var db = CreateDb();
        var known = await SeedOfferAsync(db, stock: 10);
        var missingProduct = Guid.NewGuid();
        var missingVendor = Guid.NewGuid();

        await new StockAdjustService(db).AdjustAsync(
        [
            new StockAdjustItem(missingProduct, missingVendor, Quantity: 4, Sign: -1),
            new StockAdjustItem(known.ProductId, known.VendorId, Quantity: 3, Sign: -1)
        ]);

        Assert.Equal(7, (await db.PriceListItems.FindAsync(known.Id))!.StockQuantity);
        Assert.Equal(1, await db.PriceListItems.CountAsync());
    }

    private static CatalogDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<PriceListItem> SeedOfferAsync(CatalogDbContext db, int stock)
    {
        var vendor = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-ADJ-1", Name = "Товар с остатком" };
        var offer = new PriceListItem
        {
            Product = product,
            Vendor = vendor,
            Price = 100m,
            LeadTimeDays = 5,
            StockQuantity = stock
        };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(offer);
        await db.SaveChangesAsync();
        return offer;
    }
}
