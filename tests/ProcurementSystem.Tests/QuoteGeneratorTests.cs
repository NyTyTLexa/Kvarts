using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Tests;

/// <summary>
/// Закрепляет ключевую логику генератора КП (уже проверенную вручную в Этапах 3/9),
/// чтобы регрессия не проскользнула молча в будущем.
/// </summary>
public class QuoteGeneratorTests
{
    private static QuoteGenerator CreateGenerator(AppDbContext db) =>
        new(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m }));

    [Fact]
    public async Task MinCost_picks_cheapest_offer()
    {
        using var db = TestDb.Create();
        var (cheap, _, product) = await SeedTwoOffersAsync(db);
        var specId = await SeedSpecAsync(db, product.Id);

        var quote = await CreateGenerator(db).GenerateAsync(specId, QuoteStrategy.MinCost);

        var line = Assert.Single(quote.Lines);
        Assert.True(line.Matched);
        Assert.Equal(cheap.Id, line.VendorId);
    }

    [Fact]
    public async Task MinLeadTime_picks_fastest_offer()
    {
        using var db = TestDb.Create();
        var (_, fast, product) = await SeedTwoOffersAsync(db);
        var specId = await SeedSpecAsync(db, product.Id);

        var quote = await CreateGenerator(db).GenerateAsync(specId, QuoteStrategy.MinLeadTime);

        var line = Assert.Single(quote.Lines);
        Assert.Equal(fast.Id, line.VendorId);
    }

    [Fact]
    public async Task Vendor_discount_takes_priority_over_manufacturer_discount()
    {
        using var db = TestDb.Create();
        var vendor = new Vendor { Name = "Единственный поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-DISC-1", Name = "Товар со скидками", Manufacturer = "ACME" };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = vendor, Price = 1000m, LeadTimeDays = 5, StockQuantity = 10 });
        // Скидка поставщика (20%) и скидка производителя (5%) активны одновременно —
        // приоритет должен остаться у скидки поставщика (см. QuoteGenerator.offersByProduct).
        db.Discounts.Add(new Discount { VendorId = vendor.Id, Percent = 20m });
        db.Discounts.Add(new Discount { Manufacturer = "ACME", Percent = 5m });
        var specId = await SeedSpecAsync(db, product.Id);

        var quote = await CreateGenerator(db).GenerateAsync(specId, QuoteStrategy.MinCost);

        var line = Assert.Single(quote.Lines);
        Assert.Equal(20m, line.DiscountPercent);
        Assert.Equal(800m, line.UnitPriceDiscounted);
    }

    [Fact]
    public async Task Unmatched_item_produces_unmatched_line_with_reason()
    {
        using var db = TestDb.Create();
        var spec = new Specification { Title = "Без сопоставления" };
        spec.Items.Add(new SpecificationItem { RawName = "Позиция без товара", Quantity = 1, ProductId = null });
        db.Specifications.Add(spec);
        await db.SaveChangesAsync();

        var quote = await CreateGenerator(db).GenerateAsync(spec.Id, QuoteStrategy.Balanced);

        var line = Assert.Single(quote.Lines);
        Assert.False(line.Matched);
        Assert.Equal(1, quote.UnmatchedPositions);
    }

    private static async Task<(Vendor Cheap, Vendor Fast, Product Product)> SeedTwoOffersAsync(AppDbContext db)
    {
        var cheap = new Vendor { Name = "Дешёвый-медленный", DefaultLeadTimeDays = 20 };
        var fast = new Vendor { Name = "Дорогой-быстрый", DefaultLeadTimeDays = 2 };
        var product = new Product { Sku = "SKU-TEST-1", Name = "Тестовый товар" };
        db.Vendors.AddRange(cheap, fast);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = cheap, Price = 100m, LeadTimeDays = 15, StockQuantity = 10 });
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = fast, Price = 500m, LeadTimeDays = 1, StockQuantity = 10 });
        await db.SaveChangesAsync();
        return (cheap, fast, product);
    }

    private static async Task<Guid> SeedSpecAsync(AppDbContext db, Guid productId)
    {
        var spec = new Specification { Title = "Тестовая спецификация" };
        spec.Items.Add(new SpecificationItem { RawName = "Позиция", Quantity = 1, ProductId = productId });
        db.Specifications.Add(spec);
        await db.SaveChangesAsync();
        return spec.Id;
    }
}
