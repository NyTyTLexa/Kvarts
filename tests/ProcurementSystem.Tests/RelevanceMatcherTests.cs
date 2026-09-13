using ProcurementSystem.Contracts.Search;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Matching;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using ProcurementSystem.Infrastructure.Quoting;
using Microsoft.Extensions.Options;

namespace ProcurementSystem.Tests;

public class RelevanceMatcherTests
{
    [Fact]
    public async Task Suggest_prefers_same_name_over_unrelated_product()
    {
        using var db = TestDb.Create();
        var cisco = new Product { Sku = "C9300-48P", Name = "Коммутатор Cisco Catalyst 9300-48P", Manufacturer = "Cisco", Category = "Сетевое оборудование / Коммутаторы" };
        var hpe = new Product { Sku = "DL380", Name = "Сервер HPE ProLiant DL380 Gen11", Manufacturer = "HPE", Category = "Серверы / Стоечные" };
        db.Products.AddRange(cisco, hpe);
        await db.SaveChangesAsync();

        var matcher = CreateMatcher(db);
        var hits = await matcher.SuggestAsync(null, "коммутатор cisco catalyst 9300", 3);

        var top = Assert.Single(hits.Take(1));
        Assert.Equal(cisco.Id, top.ProductId);
        Assert.True(top.Probability > 0.5, $"P={top.Probability}");
        Assert.True(top.NameCosine > 0.2);
    }

    [Fact]
    public async Task Suggest_exact_sku_is_first_with_high_probability()
    {
        using var db = TestDb.Create();
        var a = new Product { Sku = "R740-16SFF", Name = "Dell PowerEdge R740", Manufacturer = "Dell", Category = "Серверы" };
        var b = new Product { Sku = "R740-8SFF", Name = "Dell PowerEdge R740 8SFF", Manufacturer = "Dell", Category = "Серверы" };
        db.Products.AddRange(a, b);
        await db.SaveChangesAsync();

        var hits = await CreateMatcher(db).SuggestAsync("R740-16SFF", "какой-то сервер", 3);

        Assert.Equal(a.Id, hits[0].ProductId);
        Assert.Equal(MatchKind.ExactSku, hits[0].Kind);
        Assert.True(hits[0].Probability >= 0.9);
    }

    [Fact]
    public async Task MlRelevance_prefers_fresh_in_stock_offer_over_stale_cheap()
    {
        using var db = TestDb.Create();
        var stale = new Vendor { Name = "Старый прайс", DefaultLeadTimeDays = 20 };
        var fresh = new Vendor { Name = "Свежий склад", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-ML-1", Name = "Тестовый товар ML", Category = "Серверы" };
        db.Vendors.AddRange(stale, fresh);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem
        {
            Product = product, Vendor = stale, Price = 100m, LeadTimeDays = 20, StockQuantity = 0,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-200), UpdatedAtUtc = DateTime.UtcNow.AddDays(-200)
        });
        db.PriceListItems.Add(new PriceListItem
        {
            Product = product, Vendor = fresh, Price = 130m, LeadTimeDays = 4, StockQuantity = 12,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1), UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        db.PriceHistory.Add(new PriceHistoryEntry
        {
            ProductId = product.Id, VendorId = stale.Id, Price = 100m, LeadTimeDays = 20,
            RecordedAtUtc = DateTime.UtcNow.AddDays(-200)
        });
        db.PriceHistory.Add(new PriceHistoryEntry
        {
            ProductId = product.Id, VendorId = fresh.Id, Price = 130m, LeadTimeDays = 4,
            RecordedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        var spec = new Specification { Title = "ML" };
        spec.Items.Add(new SpecificationItem { RawName = "Тестовый товар ML", Quantity = 2, ProductId = product.Id });
        db.Specifications.Add(spec);
        await db.SaveChangesAsync();

        var gen = new QuoteGenerator(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m }), CreateMatcher(db));
        var quote = await gen.GenerateAsync(spec.Id, QuoteStrategy.MlRelevance);

        var line = Assert.Single(quote.Lines);
        Assert.True(line.Matched);
        Assert.Equal(fresh.Id, line.VendorId);
        Assert.Contains("ML-актуальность", line.SelectionReason);
    }

    [Fact]
    public async Task Quote_uses_analog_when_spec_item_has_no_product()
    {
        using var db = TestDb.Create();
        var vendor = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "CAT-1", Name = "Коммутатор Cisco Catalyst 9200", Manufacturer = "Cisco", Category = "Сеть / Коммутаторы" };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = vendor, Price = 50_000m, LeadTimeDays = 5, StockQuantity = 3 });
        var spec = new Specification { Title = "Аналог" };
        spec.Items.Add(new SpecificationItem { RawName = "коммутатор cisco catalyst", RawSku = null, Quantity = 1, ProductId = null });
        db.Specifications.Add(spec);
        await db.SaveChangesAsync();

        var gen = new QuoteGenerator(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m }), CreateMatcher(db));
        var quote = await gen.GenerateAsync(spec.Id, QuoteStrategy.MlRelevance);

        var line = Assert.Single(quote.Lines);
        Assert.True(line.Matched);
        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(vendor.Id, line.VendorId);
    }

    [Fact]
    public void Cosine_of_identical_strings_is_high()
    {
        var idx = new TfIdfIndex();
        var id = Guid.NewGuid();
        idx.Build([(id, "Коммутатор Cisco Catalyst 9300-48P PoE+")]);
        var v = idx.Vectorize("Коммутатор Cisco Catalyst 9300-48P PoE+");
        Assert.True(idx.Cosine(id, v) > 0.99);
    }

    private static RelevanceMatcher CreateMatcher(AppDbContext db) =>
        new(db, new SilentSearch(), new MatchingModelCache());

    private sealed class SilentSearch : ISearchEngine
    {
        public Task IndexAsync<T>(string index, IEnumerable<T> documents, CancellationToken ct = default) where T : class => Task.CompletedTask;
        public Task DeleteAsync(string index, string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string index, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<T>> SearchAsync<T>(string index, string query, int limit = 20, CancellationToken ct = default) where T : class =>
            Task.FromResult<IReadOnlyList<T>>([]);
    }
}
