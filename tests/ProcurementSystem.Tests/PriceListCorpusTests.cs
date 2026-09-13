using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Matching;
using ProcurementSystem.Infrastructure.Seeding;

namespace ProcurementSystem.Tests;

public class PriceListCorpusTests
{
    [Fact]
    public async Task Seed_creates_many_vendor_lists_and_dirty_spec()
    {
        using var db = TestDb.Create();
        var result = await new PriceListCorpus(db).SeedAsync(24);

        Assert.Equal(24, result.Vendors);
        Assert.True(result.Products >= 80);
        Assert.True(result.Offers > result.Products);
        Assert.Equal(24, result.Uploads);
        Assert.True(result.SpecItems >= 40);
        Assert.True(result.SpecUnmatched >= 10);

        var again = await new PriceListCorpus(db).SeedAsync(24);
        Assert.Equal(result.Vendors, again.Vendors);
        Assert.Equal(result.SpecificationId, again.SpecificationId);
    }

    [Fact]
    public async Task Dirty_spec_is_matchable_by_ml()
    {
        using var db = TestDb.Create();
        var result = await new PriceListCorpus(db).SeedAsync(24);
        var matcher = new RelevanceMatcher(db, new SilentSearch(), new MatchingModelCache());
        var spec = await db.Specifications.Include(s => s.Items).FirstAsync(s => s.Id == result.SpecificationId);

        var hits = 0;
        var exact = 0;
        foreach (var item in spec.Items)
        {
            var suggestions = await matcher.SuggestAsync(item.RawSku, item.RawName, 3);
            if (suggestions.Count == 0) continue;
            if (suggestions[0].Kind == MatchKind.ExactSku) exact++;
            if (suggestions[0].Probability >= 0.40) hits++;
        }

        Assert.True(exact >= 10, $"exact SKU hits {exact}");
        Assert.True(hits >= spec.Items.Count / 2, $"ML hits {hits}/{spec.Items.Count}");
    }

    [Fact]
    public async Task Large_catalog_still_finds_cisco_among_distractors()
    {
        using var db = TestDb.Create();
        var result = await new PriceListCorpus(db).SeedAsync(20, 900);
        Assert.True(result.Products >= 900);
        Assert.True(result.Offers > 2000);

        var matcher = new RelevanceMatcher(db, new SilentSearch(), new MatchingModelCache());
        var hits = await matcher.SuggestAsync("C9300-XX", "коммутатор cisco catalyst 9300 48 портов poe", 5);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Sku.Contains("C9300", StringComparison.OrdinalIgnoreCase));
        Assert.True(hits[0].Probability >= 0.35, $"top P={hits[0].Probability} sku={hits[0].Sku}");
    }

    private sealed class SilentSearch : ISearchEngine
    {
        public Task IndexAsync<T>(string index, IEnumerable<T> documents, CancellationToken ct = default) where T : class => Task.CompletedTask;
        public Task DeleteAsync(string index, string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string index, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<T>> SearchAsync<T>(string index, string query, int limit = 20, CancellationToken ct = default) where T : class =>
            Task.FromResult<IReadOnlyList<T>>([]);
    }
}
