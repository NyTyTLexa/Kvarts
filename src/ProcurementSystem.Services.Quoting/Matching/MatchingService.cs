using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Quoting.Clients;
using ProcurementSystem.Services.Quoting.Persistence;

namespace ProcurementSystem.Services.Quoting.Matching;

public interface IMatchingService
{
    Task<SpecMatchDto?> PreviewAsync(Guid specId, CancellationToken ct = default);
    Task<SpecMatchDto?> ApplyBestAsync(Guid specId, double minProbability = 0.5, CancellationToken ct = default);
    Task<bool> ApplyOneAsync(Guid specId, Guid itemId, Guid productId, CancellationToken ct = default);
}

public sealed class MatchingService(QuotingDbContext db, IRelevanceMatcher matcher, ICatalogClient catalog) : IMatchingService
{
    public Task<SpecMatchDto?> PreviewAsync(Guid specId, CancellationToken ct = default) =>
        BuildAsync(specId, persistBest: false, minProbability: 0, ct);

    public Task<SpecMatchDto?> ApplyBestAsync(Guid specId, double minProbability = 0.5, CancellationToken ct = default) =>
        BuildAsync(specId, persistBest: true, minProbability, ct);

    public async Task<bool> ApplyOneAsync(Guid specId, Guid itemId, Guid productId, CancellationToken ct = default)
    {
        var item = await db.SpecificationItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.SpecificationId == specId, ct);
        if (item is null) return false;
        if (!await catalog.ProductExistsAsync(productId, ct)) return false;
        item.ProductId = productId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<SpecMatchDto?> BuildAsync(Guid specId, bool persistBest, double minProbability, CancellationToken ct)
    {
        var spec = await db.Specifications.Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == specId, ct);
        if (spec is null) return null;

        var sw = Stopwatch.StartNew();
        await matcher.EnsureReadyAsync(ct);
        var catalogSize = matcher.TrainedOn;
        var details = new List<ItemMatchDto>(spec.Items.Count);

        foreach (var item in spec.Items.OrderBy(i => i.RawName))
        {
            var suggestions = await matcher.SuggestAsync(item.RawSku, item.RawName, 5, ct);
            if (persistBest && item.ProductId is null)
            {
                var best = suggestions.FirstOrDefault(s => s.Probability >= minProbability);
                if (best is not null)
                    item.ProductId = best.ProductId;
            }
            details.Add(new ItemMatchDto(
                item.Id, item.RawSku, item.RawName, item.Quantity,
                item.ProductId, item.ProductId is not null, suggestions));
        }

        if (persistBest) await db.SaveChangesAsync(ct);

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
