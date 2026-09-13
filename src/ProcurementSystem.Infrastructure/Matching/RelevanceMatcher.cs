using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Search;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Search;

namespace ProcurementSystem.Infrastructure.Matching;

internal sealed record CatalogRow(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);

/// <summary>Кэш TF-IDF и весов логрегрессии — один на процесс, пересчитывается при изменении каталога.</summary>
internal sealed class MatchingModelCache
{
    public readonly SemaphoreSlim Gate = new(1, 1);
    public TfIdfIndex Index { get; set; } = new();
    public LogisticRegression Model { get; set; } = new();
    public Dictionary<Guid, CatalogRow> Rows { get; set; } = [];
    public Dictionary<string, Guid> Sku { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<Guid>> ByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int TrainedOn { get; set; }
}

internal sealed class RelevanceMatcher(AppDbContext db, ISearchEngine search, MatchingModelCache cache) : IRelevanceMatcher
{
    public int TrainedOn => cache.TrainedOn;

    public async Task EnsureReadyAsync(CancellationToken ct = default)
    {
        var count = await db.Products.CountAsync(ct);
        if (count == cache.TrainedOn && cache.Rows.Count > 0) return;
        await cache.Gate.WaitAsync(ct);
        try
        {
            count = await db.Products.CountAsync(ct);
            if (count == cache.TrainedOn && cache.Rows.Count > 0) return;
            await RebuildAsync(ct);
        }
        finally { cache.Gate.Release(); }
    }

    public async Task<IReadOnlyList<MatchSuggestion>> SuggestAsync(
        string? sku, string name, int take = 5, CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct);
        take = Math.Clamp(take, 1, 15);
        var hits = new Dictionary<Guid, MatchSuggestion>();

        if (!string.IsNullOrWhiteSpace(sku) && cache.Sku.TryGetValue(sku.Trim(), out var exactId)
            && cache.Rows.TryGetValue(exactId, out var exact))
        {
            hits[exactId] = ToSuggestion(exact, sku, name, MatchKind.ExactSku, forceP: 0.99);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var id in await CollectCandidatesAsync(sku, name, ct))
            {
                if (hits.ContainsKey(id) || !cache.Rows.TryGetValue(id, out var row)) continue;
                hits[id] = ToSuggestion(row, sku, name, KindOf(sku, name, row));
            }
        }

        return hits.Values
            .OrderByDescending(h => h.Kind == MatchKind.ExactSku)
            .ThenByDescending(h => h.Probability)
            .Take(take)
            .ToList();
    }

    internal double[] Features(string? sku, string name, CatalogRow row)
    {
        var qVec = cache.Index.Vectorize(name);
        var cosine = cache.Index.Cosine(row.Id, qVec);
        var skuExact = !string.IsNullOrWhiteSpace(sku)
                       && string.Equals(sku.Trim(), row.Sku, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        var skuFuzzy = string.IsNullOrWhiteSpace(sku) ? 0.0 : TextFeatures.NormalizedLevenshtein(sku, row.Sku);
        var jaccard = TextFeatures.Jaccard(name, row.Name);
        var sameCat = SameCategory(name, row) ? 1.0 : 0.0;
        // sameCategory признак по листовой категории товара vs токены запроса слабый;
        // используем совпадение категории только если запрос уже близок по имени — иначе 0.
        if (cosine < 0.12 && jaccard < 0.15) sameCat = 0;
        var sameMfr = !string.IsNullOrWhiteSpace(row.Manufacturer)
                      && TextFeatures.Normalize(name).Contains(TextFeatures.Normalize(row.Manufacturer))
            ? 1.0 : 0.0;
        return [skuExact, skuFuzzy, cosine, jaccard, sameCat, sameMfr];
    }

    private MatchKind KindOf(string? sku, string name, CatalogRow row)
    {
        var x = Features(sku, name, row);
        if (x[0] >= 1) return MatchKind.ExactSku;
        if (x[2] >= 0.45 || x[3] >= 0.40 || x[1] >= 0.85) return MatchKind.FuzzyName;
        return MatchKind.Analog;
    }

    private MatchSuggestion ToSuggestion(CatalogRow row, string? sku, string name, MatchKind kind, double? forceP = null)
    {
        var x = Features(sku, name, row);
        var p = forceP ?? cache.Model.Predict(x);
        var cosine = x[2];
        var reason = kind switch
        {
            MatchKind.ExactSku => $"Точный артикул {row.Sku}",
            MatchKind.Analog => $"Аналог · {row.Category} · P={p:0.00}, косинус {cosine:0.00}",
            _ => $"ML (TF-IDF + логрегрессия) · P={p:0.00}, косинус {cosine:0.00}"
        };
        return new MatchSuggestion(row.Id, row.Sku, row.Name, row.Manufacturer, row.Category, kind, p, cosine, reason);
    }

    private static bool SameCategory(string name, CatalogRow row) =>
        !string.IsNullOrWhiteSpace(row.Category)
        && TextFeatures.Tokens(name).Any(t => t.Length > 3 && t[0] != '#'
            && TextFeatures.Normalize(row.Category).Contains(t));

    private async Task<IEnumerable<Guid>> CollectCandidatesAsync(string? sku, string name, CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        foreach (var id in cache.Index.Candidates(name, 40)) ids.Add(id);

        try
        {
            var hits = await search.SearchAsync<ProductSearchDocument>(ProductIndexer.Index, name, 12, ct);
            foreach (var h in hits)
                if (Guid.TryParse(h.Id, out var hid)) ids.Add(hid);
        }
        catch
        {
            // индекс опционален: TF-IDF сам собирает кандидатов
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var prefix = sku.Trim();
            if (prefix.Length >= 3)
            {
                foreach (var (s, id) in cache.Sku)
                    if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || prefix.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                        ids.Add(id);
            }
        }

        // аналоги: категория лучших по косинусу
        var qVec = cache.Index.Vectorize(name);
        var best = ids
            .Select(id => cache.Rows.TryGetValue(id, out var r) ? r : null)
            .Where(r => r is not null)
            .OrderByDescending(r => cache.Index.Cosine(r!.Id, qVec))
            .FirstOrDefault();
        if (best?.Category is { Length: > 0 } cat && cache.ByCategory.TryGetValue(cat, out var cousins))
            foreach (var id in cousins.Take(20)) ids.Add(id);

        return ids;
    }

    private async Task RebuildAsync(CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Select(p => new CatalogRow(p.Id, p.Sku, p.Name, p.Manufacturer, p.Category))
            .ToListAsync(ct);

        cache.Rows = products.ToDictionary(p => p.Id);
        cache.Sku = products
            .GroupBy(p => p.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        cache.ByCategory = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .GroupBy(p => p.Category!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        cache.Index = new TfIdfIndex();
        cache.Index.Build(products.Select(p => (p.Id, p.Name)));

        cache.Model = new LogisticRegression();
        if (products.Count >= 4)
            cache.Model.Train(BuildTrainingSet(products));

        cache.TrainedOn = products.Count;
    }

    private List<(double[] X, double Y)> BuildTrainingSet(IReadOnlyList<CatalogRow> products)
    {
        var rng = new Random(42);
        var n = products.Count;
        var take = Math.Min(n, 2000);
        var sample = take == n ? products : Stratify(products, take);
        var samples = new List<(double[] X, double Y)>(sample.Count * 3);
        foreach (var p in sample)
        {
            samples.Add((Features(p.Sku, p.Name, p), 1));
            samples.Add((Features(p.Sku, TextFeatures.Typo(p.Name, rng), p), 1));
            CatalogRow other;
            do { other = products[rng.Next(n)]; } while (other.Id == p.Id && n > 1);
            samples.Add((Features(p.Sku, p.Name, other), 0));
        }
        return samples;
    }

    static List<CatalogRow> Stratify(IReadOnlyList<CatalogRow> products, int take)
    {
        var groups = products
            .GroupBy(p => p.Category ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.ToList())
            .ToList();
        var picked = new List<CatalogRow>(take);
        for (var i = 0; picked.Count < take; i++)
        {
            var added = false;
            foreach (var g in groups)
            {
                if (i >= g.Count) continue;
                picked.Add(g[i]);
                added = true;
                if (picked.Count == take) break;
            }
            if (!added) break;
        }
        return picked;
    }
}
