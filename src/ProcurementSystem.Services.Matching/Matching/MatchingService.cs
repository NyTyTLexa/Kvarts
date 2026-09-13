using System.Diagnostics;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Matching.Clients;

namespace ProcurementSystem.Services.Matching.Matching;

public interface IMatchingService
{
    Task<SpecMatchDto?> PreviewAsync(Guid specId, CancellationToken ct = default);
    Task<SpecMatchDto?> ApplyBestAsync(Guid specId, double minProbability = 0.5, CancellationToken ct = default);
    Task<bool> ApplyOneAsync(Guid specId, Guid itemId, Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<MatchSuggestion>> SuggestAsync(string? sku, string name, int take = 5, CancellationToken ct = default);
}

public sealed class MatchingService(IQuotingClient quoting, IRelevanceMatcher matcher, ICatalogClient catalog) : IMatchingService
{
    public Task<SpecMatchDto?> PreviewAsync(Guid specId, CancellationToken ct = default) =>
        BuildAsync(specId, persistBest: false, minProbability: 0, ct);

    public Task<SpecMatchDto?> ApplyBestAsync(Guid specId, double minProbability = 0.5, CancellationToken ct = default) =>
        BuildAsync(specId, persistBest: true, minProbability, ct);

    public async Task<bool> ApplyOneAsync(Guid specId, Guid itemId, Guid productId, CancellationToken ct = default)
    {
        var spec = await quoting.GetSpecificationAsync(specId, ct);
        if (spec is null) return false;
        var items = spec.Items ?? [];
        if (items.All(i => i.Id != itemId)) return false;
        if (!await catalog.ProductExistsAsync(productId, ct)) return false;
        return await quoting.SetItemProductAsync(specId, itemId, productId, ct);
    }

    public Task<IReadOnlyList<MatchSuggestion>> SuggestAsync(
        string? sku, string name, int take = 5, CancellationToken ct = default) =>
        matcher.SuggestAsync(sku, name, take, ct);

    private async Task<SpecMatchDto?> BuildAsync(Guid specId, bool persistBest, double minProbability, CancellationToken ct)
    {
        var spec = await quoting.GetSpecificationAsync(specId, ct);
        if (spec is null) return null;

        var sw = Stopwatch.StartNew();
        await matcher.EnsureReadyAsync(ct);
        var catalogSize = matcher.TrainedOn;
        var items = spec.Items ?? [];
        var details = new List<ItemMatchDto>(items.Count);

        foreach (var item in items.OrderBy(i => i.Name))
        {
            var suggestions = await matcher.SuggestAsync(item.Sku, item.Name, 5, ct);
            var productId = item.ProductId;
            if (persistBest && productId is null)
            {
                var best = suggestions.FirstOrDefault(s => s.Probability >= minProbability);
                if (best is not null && await quoting.SetItemProductAsync(specId, item.Id, best.ProductId, ct))
                    productId = best.ProductId;
            }
            details.Add(new ItemMatchDto(
                item.Id, item.Sku, item.Name, item.Quantity,
                productId, productId is not null, suggestions));
        }

        var matched = details.Count(d => d.Matched);
        var top = details.Select(d => d.Suggestions.FirstOrDefault()?.Kind ?? MatchKind.None).ToList();
        return new SpecMatchDto(spec.Id, spec.Title, details.Count, matched, details.Count - matched,
            ModelReady: catalogSize > 0, catalogSize, details,
            (int)sw.ElapsedMilliseconds, matcher.TrainedOn,
            top.Count(k => k == MatchKind.ExactSku),
            top.Count(k => k == MatchKind.FuzzyName),
            top.Count(k => k == MatchKind.Analog));
    }
}
