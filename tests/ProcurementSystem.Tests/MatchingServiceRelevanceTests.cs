using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Matching.Clients;
using ProcurementSystem.Services.Matching.Matching;

namespace ProcurementSystem.Tests;

public class MatchingServiceRelevanceTests
{
    [Fact]
    public async Task MatchingService_Suggest_prefers_same_name_over_unrelated_product()
    {
        var cisco = new CatalogProductSnapshot(
            Guid.NewGuid(), "C9300-48P", "Коммутатор Cisco Catalyst 9300-48P",
            "Cisco", "Сетевое оборудование / Коммутаторы");
        var hpe = new CatalogProductSnapshot(
            Guid.NewGuid(), "DL380", "Сервер HPE ProLiant DL380 Gen11",
            "HPE", "Серверы / Стоечные");
        var fakeCatalog = new FakeCatalogClient(cisco, hpe);

        var cache = new MatchingModelCache();
        var matcher = new RelevanceMatcher(fakeCatalog, cache);
        var hits = await matcher.SuggestAsync(null, "коммутатор cisco catalyst 9300", 3);

        var top = Assert.Single(hits.Take(1));
        Assert.Equal(cisco.Id, top.ProductId);
        Assert.True(top.Probability > 0.5, $"P={top.Probability}");
        Assert.True(top.NameCosine > 0.2);
    }

    [Fact]
    public async Task MatchingService_Suggest_exact_sku_is_first_with_high_probability()
    {
        var a = new CatalogProductSnapshot(
            Guid.NewGuid(), "R740-16SFF", "Dell PowerEdge R740", "Dell", "Серверы");
        var b = new CatalogProductSnapshot(
            Guid.NewGuid(), "R740-8SFF", "Dell PowerEdge R740 8SFF", "Dell", "Серверы");
        var fakeCatalog = new FakeCatalogClient(a, b);

        var cache = new MatchingModelCache();
        var matcher = new RelevanceMatcher(fakeCatalog, cache);
        var hits = await matcher.SuggestAsync("R740-16SFF", "какой-то сервер", 3);

        Assert.Equal(a.Id, hits[0].ProductId);
        Assert.Equal(MatchKind.ExactSku, hits[0].Kind);
        Assert.True(hits[0].Probability >= 0.9);
    }

    [Fact]
    public void MatchingService_Cosine_of_identical_strings_is_high()
    {
        var idx = new TfIdfIndex();
        var id = Guid.NewGuid();
        idx.Build([(id, "Коммутатор Cisco Catalyst 9300-48P PoE+")]);
        var v = idx.Vectorize("Коммутатор Cisco Catalyst 9300-48P PoE+");
        Assert.True(idx.Cosine(id, v) > 0.99);
    }

    private sealed class FakeCatalogClient : ICatalogClient
    {
        private readonly List<CatalogProductSnapshot> _products;

        public FakeCatalogClient(params CatalogProductSnapshot[] products) =>
            _products = [.. products];

        public Task<int> CountProductsAsync(CancellationToken ct = default) =>
            Task.FromResult(_products.Count);

        public Task<IReadOnlyList<CatalogProductSnapshot>> ListAllProductsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CatalogProductSnapshot>>(_products);

        public Task<bool> ProductExistsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_products.Exists(p => p.Id == id));
    }
}
