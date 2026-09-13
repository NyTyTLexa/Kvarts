using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Services.Retail.Clients;
using ProcurementSystem.Services.Retail.Persistence;
using ProcurementSystem.Services.Retail.Retail;

namespace ProcurementSystem.Tests;

public class RetailServiceTests
{
    [Fact]
    public void Extractor_reads_json_ld_product()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {"@context":"https://schema.org","@type":"Product","name":"Коммутатор Cisco Catalyst 9300",
             "brand":{"@type":"Brand","name":"Cisco"},"sku":"C9300-48P",
             "offers":{"@type":"Offer","price":"189990","priceCurrency":"RUB","availability":"https://schema.org/InStock"}}
            </script></head></html>
            """;
        var hit = StructuredDataExtractor.Extract(html, "https://shop.example/product/c9300");
        Assert.NotNull(hit);
        Assert.Equal("Коммутатор Cisco Catalyst 9300", hit.Name);
        Assert.Equal("Cisco", hit.Brand);
        Assert.Equal("C9300-48P", hit.Sku);
        Assert.Equal(189990m, hit.Price);
        Assert.True(hit.InStock);
        Assert.Equal("JsonLd", hit.Source);
    }

    [Fact]
    public void Extractor_reads_opengraph_price()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="Ноутбук ThinkPad T14">
            <meta property="og:price:amount" content="124990.00">
            <meta property="og:price:currency" content="RUB">
            </head></html>
            """;
        var hit = StructuredDataExtractor.Extract(html, "https://nix.ru/catalog/thinkpad");
        Assert.NotNull(hit);
        Assert.Equal("Ноутбук ThinkPad T14", hit.Name);
        Assert.Equal(124990m, hit.Price);
        Assert.Equal("OpenGraph", hit.Source);
        Assert.Equal("NIX", hit.Shop);
    }

    [Fact]
    public void Extractor_skips_social_hosts()
    {
        const string html = """<html><head><meta property="og:title" content="x"><meta property="og:price:amount" content="10"></head></html>""";
        Assert.Null(StructuredDataExtractor.Extract(html, "https://vk.com/wall-1"));
    }

    [Fact]
    public void Wildberries_parses_kopeck_prices()
    {
        const string json = """
            {"data":{"products":[
              {"id":123,"name":"iPhone 16 128GB","brand":"Apple","salePriceU":7999000,"totalQuantity":4},
              {"id":124,"name":"без цены","brand":"X","salePriceU":0,"totalQuantity":1}
            ]}}
            """;
        var hits = WildberriesCatalogClient.Parse(json, 8);
        var hit = Assert.Single(hits);
        Assert.Equal("WB-123", hit.Sku);
        Assert.Equal(79990m, hit.Price);
        Assert.True(hit.InStock);
        Assert.Contains("123", hit.Url);
    }

    [Fact]
    public void Wildberries_parses_v18_size_price()
    {
        const string json = """
            {"data":{"products":[
              {"id":587606854,"name":"Коммутатор C9300-48P-E","brand":"Cisco",
               "totalQuantity":3,"sizes":[{"price":{"product":28159900,"total":28159900}}]}
            ]}}
            """;
        var hit = Assert.Single(WildberriesCatalogClient.Parse(json, 8));
        Assert.Equal("WB-587606854", hit.Sku);
        Assert.Equal(281599m, hit.Price);
        Assert.True(hit.InStock);
    }

    [Fact]
    public void DuckDuckGo_unwraps_uddg_and_skips_blocked()
    {
        const string html = """
            <a class="result__a" href="https://duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.citilink.ru%2Fproduct%2Fiphone-16">Citilink</a>
            <a class="result__a" href="https://youtube.com/watch?v=1">video</a>
            """;
        var links = DuckDuckGoDiscovery.Parse(html, 8);
        var link = Assert.Single(links);
        Assert.Equal("www.citilink.ru", link.Host);
        Assert.Contains("/product/", link.AbsolutePath);
    }

    [Fact]
    public async Task Search_uses_wb_and_discovers_json_ld_shop()
    {
        var handler = new MapHandler
        {
            ["search.wb.ru"] = WbJson(),
            ["duckduckgo.com"] = """<a class="result__a" href="https://shop.test/product/sw">sw</a>""",
            ["shop.test"] = """
                <script type="application/ld+json">
                {"@type":"Product","name":"Коммутатор тестовый","sku":"SW-1",
                 "offers":{"price":10000,"priceCurrency":"RUB","availability":"InStock"}}
                </script>
                """,
        };
        var http = new RetailHttpClient(RetailHttp.Create(handler));
        await using var db = CreateDb();
        var svc = new RetailSearchService(http, new WildberriesCatalogClient(http), new DuckDuckGoDiscovery(http), db);

        var result = await svc.SearchAsync(new RetailSearchRequest { Q = "коммутатор", KnownShops = false });

        Assert.Contains(result.Hits, h => h.Source == "Wildberries");
        Assert.Contains(result.Hits, h => h.Host == "shop.test" && h.Price == 10000m);
        Assert.Contains(result.Shops, s => s.Host == "shop.test" && s.Kind == "Discovered");
    }

    [Fact]
    public async Task Search_hits_wb_and_ddg_in_parallel()
    {
        var handler = new MapHandler
        {
            Delay = TimeSpan.FromMilliseconds(180),
            ["search.wb.ru"] = WbJson(),
            ["duckduckgo.com"] = """<a class="result__a" href="https://shop.test/product/sw">sw</a>""",
            ["shop.test"] = """
                <script type="application/ld+json">
                {"@type":"Product","name":"Коммутатор тестовый","sku":"SW-1",
                 "offers":{"price":10000,"priceCurrency":"RUB"}}
                </script>
                """,
        };
        var http = new RetailHttpClient(RetailHttp.Create(handler));
        await using var db = CreateDb();
        var svc = new RetailSearchService(http, new WildberriesCatalogClient(http), new DuckDuckGoDiscovery(http), db);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.SearchAsync(new RetailSearchRequest { Q = "коммутатор", KnownShops = false });
        sw.Stop();

        Assert.True(handler.MaxInflight >= 2, $"ожидали пачку запросов, max={handler.MaxInflight}");
        Assert.True(sw.ElapsedMilliseconds < 700, $"поиск шёл {sw.ElapsedMilliseconds} мс — похоже на последовательность");
        Assert.Contains(result.Hits, h => h.Source == "Wildberries");
        Assert.Contains(result.Hits, h => h.Host == "shop.test");
    }

    [Fact]
    public async Task Import_creates_vendor_product_and_offer_via_catalog()
    {
        var catalog = new FakeCatalog();
        var svc = new RetailImportService(catalog);
        var hits = new[]
        {
            new RetailHitDto("Wildberries", "wildberries.ru", "iPhone 16 128GB", "Apple", "WB-1", 79990m, "RUB", true,
                "https://www.wildberries.ru/catalog/1/detail.aspx", "Wildberries"),
        };

        var r = await svc.ImportAsync(hits);

        Assert.Equal(1, r.VendorsCreated);
        Assert.Equal(1, r.ProductsCreated);
        Assert.Equal(1, r.OffersUpserted);
        var vendor = Assert.Single(catalog.Vendors);
        Assert.Equal("Wildberries", vendor.Name);
        var product = Assert.Single(catalog.Products);
        Assert.Equal("WB-1", product.Sku);
        Assert.Equal("Розница", product.Category);
        var offer = Assert.Single(catalog.Offers);
        Assert.Equal(79990m, offer.Price);
        Assert.Equal(vendor.Id, offer.VendorId);
        Assert.Equal(product.Id, offer.ProductId);
    }

    [Fact]
    public async Task Import_reuses_existing_catalog_sku()
    {
        var catalog = new FakeCatalog();
        var existing = await catalog.CreateProductAsync("WB-1", "уже есть", "Apple", "Смартфоны");
        var svc = new RetailImportService(catalog);

        var r = await svc.ImportAsync(
        [
            new RetailHitDto("Wildberries", "wildberries.ru", "iPhone 16 128GB", "Apple", "WB-1", 79990m, "RUB", true,
                "https://www.wildberries.ru/catalog/1/detail.aspx", "Wildberries"),
        ]);

        Assert.Equal(0, r.ProductsCreated);
        Assert.Equal(1, r.VendorsCreated);
        Assert.Equal(1, r.OffersUpserted);
        Assert.Equal(existing.Id, Assert.Single(catalog.Products).Id);
    }

    static RetailDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<RetailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    static string WbJson() => """{"data":{"products":[{"id":9,"name":"Коммутатор WB","brand":"Cisco","salePriceU":15000000,"totalQuantity":2}]}}""";

    sealed class MapHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Map { get; init; } = new();
        public TimeSpan Delay { get; init; }
        public int MaxInflight;
        int _inflight;

        public string this[string host]
        {
            set => Map[host] = value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _inflight);
            if (n > MaxInflight) MaxInflight = n;
            try
            {
                if (Delay > TimeSpan.Zero)
                    await Task.Delay(Delay, cancellationToken);
                var url = request.RequestUri?.ToString() ?? "";
                foreach (var (key, body) in Map)
                {
                    if (!url.Contains(key, StringComparison.OrdinalIgnoreCase)) continue;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "text/html"),
                        RequestMessage = request,
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
            }
            finally { Interlocked.Decrement(ref _inflight); }
        }
    }

    sealed class FakeCatalog : ICatalogClient
    {
        public List<CatalogVendorDto> Vendors { get; } = [];
        public List<CatalogProductDto> Products { get; } = [];
        public List<CatalogOfferDto> Offers { get; } = [];

        public Task<CatalogVendorDto?> FindVendorByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Vendors.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<CatalogVendorDto> CreateVendorAsync(string name, int defaultLeadTimeDays, CancellationToken ct = default)
        {
            var v = new CatalogVendorDto(Guid.NewGuid(), name, null, defaultLeadTimeDays);
            Vendors.Add(v);
            return Task.FromResult(v);
        }

        public Task<CatalogProductDto?> FindBySkuAsync(string sku, CancellationToken ct = default) =>
            Task.FromResult(Products.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase)));

        public Task<CatalogProductDto?> SuggestByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Products.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<CatalogProductDto> CreateProductAsync(string sku, string name, string? manufacturer, string? category, CancellationToken ct = default)
        {
            var p = new CatalogProductDto(Guid.NewGuid(), sku, name, manufacturer, category);
            Products.Add(p);
            return Task.FromResult(p);
        }

        public Task<CatalogOfferDto?> FindOfferAsync(Guid productId, Guid vendorId, CancellationToken ct = default) =>
            Task.FromResult(Offers.FirstOrDefault(o => o.ProductId == productId && o.VendorId == vendorId));

        public Task<CatalogOfferDto?> CreateOfferAsync(Guid productId, Guid vendorId, decimal price, int leadTimeDays, int stockQuantity, CancellationToken ct = default)
        {
            var o = new CatalogOfferDto(Guid.NewGuid(), productId, vendorId, "v", price, "RUB", leadTimeDays, stockQuantity, null);
            Offers.Add(o);
            return Task.FromResult<CatalogOfferDto?>(o);
        }
    }
}
