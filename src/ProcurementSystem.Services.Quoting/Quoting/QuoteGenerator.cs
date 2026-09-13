using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Quoting.Clients;
using ProcurementSystem.Services.Quoting.Matching;
using ProcurementSystem.Services.Quoting.Observability;
using ProcurementSystem.Services.Quoting.Persistence;

namespace ProcurementSystem.Services.Quoting.Quoting;

/// <summary>Стратегия подбора офферов под спецификацию.</summary>
public enum QuoteStrategy
{
    MinCost,
    MinLeadTime,
    Balanced,
    MlRelevance
}

/// <summary>
/// Одна строка КП: выбранный под позицию спецификации оффер с учётом скидки (ТЗ п.5.4).
/// UnitPrice — исходная цена без скидки (без НДС); UnitPriceDiscounted — цена со скидкой (без НДС);
/// LineTotal — итог со скидкой (без НДС); *WithVat* — те же величины с учётом ставки НДС.
/// </summary>
public record QuoteLine(
    Guid SpecificationItemId,
    Guid? ProductId,
    string? Sku,
    string Name,
    int Quantity,
    bool Matched,
    Guid? VendorId,
    string? VendorName,
    decimal UnitPrice,
    decimal LineTotal,
    int LeadTimeDays,
    int StockQuantity,
    int CandidateOffers,
    decimal DiscountPercent,
    decimal UnitPriceDiscounted,
    decimal UnitPriceWithVat,
    decimal UnitPriceWithVatDiscounted,
    decimal LineTotalWithVat,
    string? Manufacturer,
    string? SelectionReason,
    string? MatchKind = null,
    double? MatchScore = null);

/// <summary>Готовое коммерческое предложение по выбранной стратегии. Не хранится таблицей — считается на лету.</summary>
public record Quote(
    Guid SpecificationId,
    string SpecificationTitle,
    QuoteStrategy Strategy,
    double WeightPrice,
    double WeightLeadTime,
    IReadOnlyList<QuoteLine> Lines,
    decimal TotalCost,
    int MaxLeadTimeDays,
    int VendorsUsed,
    int MatchedPositions,
    int UnmatchedPositions,
    decimal VatRate,
    decimal TotalCostWithVat);

public interface IQuoteGenerator
{
    Task<Quote> GenerateAsync(Guid specificationId, QuoteStrategy strategy,
        double weightPrice = 0.5, double weightLeadTime = 0.5, bool onlyInStock = false,
        CancellationToken ct = default);

    Task<IReadOnlyList<Quote>> GenerateAllAsync(Guid specificationId,
        double weightPrice = 0.5, double weightLeadTime = 0.5, bool onlyInStock = false,
        CancellationToken ct = default);
}

file readonly record struct ItemBind(Guid? ProductId, MatchKind Kind, double Score, string? Reason);

public class QuoteOptions
{
    public decimal VatRate { get; set; } = 20m;
}

/// <summary>
/// Ядро «изюминки» системы: по спецификации и каталогу офферов подбирает поставщиков
/// под выбранную стратегию и считает итоговые показатели КП.
/// Товары и офферы — HTTP к Catalog, не свой DbContext.
/// </summary>
public class QuoteGenerator(
    QuotingDbContext db,
    ICatalogClient catalog,
    IOptions<QuoteOptions> opts,
    IRelevanceMatcher? matcher = null) : IQuoteGenerator
{
    private readonly decimal _vatRate = opts.Value.VatRate;
    private record Candidate(Guid VendorId, string VendorName, decimal Price, int LeadTimeDays, int StockQuantity, decimal DiscountPercent, decimal EffectivePrice, DateTime LastPriceAtUtc);

    public async Task<Quote> GenerateAsync(Guid specificationId, QuoteStrategy strategy,
        double weightPrice = 0.5, double weightLeadTime = 0.5, bool onlyInStock = false,
        CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("quote.generate");
        activity?.SetTag("spec.id", specificationId.ToString());
        activity?.SetTag("quote.strategy", strategy.ToString());
        activity?.SetTag("quote.only_in_stock", onlyInStock);

        var spec = await db.Specifications
            .Include(s => s.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == specificationId, ct)
            ?? throw new InvalidOperationException($"Спецификация {specificationId} не найдена");

        var binds = new Dictionary<Guid, ItemBind>();
        foreach (var item in spec.Items)
        {
            if (item.ProductId is { } already)
            {
                binds[item.Id] = new ItemBind(already, MatchKind.ExactSku, 1, null);
                continue;
            }
            if (matcher is null)
            {
                binds[item.Id] = new ItemBind(null, MatchKind.None, 0, null);
                continue;
            }
            var sug = (await matcher.SuggestAsync(item.RawSku, item.RawName, 1, ct)).FirstOrDefault();
            if (sug is not null && sug.Probability >= 0.40)
                binds[item.Id] = new ItemBind(sug.ProductId, sug.Kind, sug.Probability, sug.Reason);
            else
                binds[item.Id] = new ItemBind(null, MatchKind.None, 0, null);
        }

        var productIds = binds.Values.Where(b => b.ProductId != null)
            .Select(b => b.ProductId!.Value).Distinct().ToList();

        var bundles = await catalog.GetBundlesAsync(productIds, ct);
        var products = bundles.ToDictionary(
            kv => kv.Key,
            kv => (kv.Value.Product.Sku, kv.Value.Product.Name, kv.Value.Product.Manufacturer));

        var itemIds = spec.Items.Select(i => i.Id).ToList();
        var overrides = await db.QuoteOverrides.AsNoTracking()
            .Where(o => itemIds.Contains(o.SpecificationItemId))
            .ToDictionaryAsync(o => o.SpecificationItemId, o => o.VendorId, ct);

        var lastPriceMap = new Dictionary<(Guid ProductId, Guid VendorId), DateTime>();
        foreach (var (pid, bundle) in bundles)
        {
            foreach (var (vendorId, at) in bundle.LastPriceAtByVendor)
                lastPriceMap[(pid, vendorId)] = at;
        }

        var vendorIds = bundles.Values.SelectMany(b => b.Offers.Select(o => o.VendorId)).Distinct().ToList();
        var manufacturers = products.Values.Select(p => p.Manufacturer)
            .Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!).Distinct().ToList();
        var applicable = await catalog.GetActiveDiscountsAsync(vendorIds, manufacturers, ct);
        var vendorDisc = applicable.Where(a => a.VendorId is not null)
            .ToDictionary(a => a.VendorId!.Value, a => a.Percent);
        var mfrDisc = applicable.Where(a => a.Manufacturer is not null)
            .GroupBy(a => a.Manufacturer!).ToDictionary(g => g.Key, g => g.First().Percent);

        var offersByProduct = bundles.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                products.TryGetValue(kv.Key, out var pr);
                var mfr = pr.Manufacturer;
                return (IReadOnlyList<Candidate>)kv.Value.Offers.Select(o =>
                {
                    decimal disc = vendorDisc.TryGetValue(o.VendorId, out var vd) ? vd
                        : (mfr is not null && mfrDisc.TryGetValue(mfr, out var md) ? md : 0m);
                    var eff = Math.Round(o.Price * (1 - disc / 100m), 2);
                    var at = lastPriceMap.TryGetValue((o.ProductId, o.VendorId), out var hist)
                        ? hist : DateTime.UtcNow;
                    return new Candidate(o.VendorId, o.VendorName, o.Price, o.LeadTimeDays, o.StockQuantity, disc, eff, at);
                }).ToList();
            });

        var vat = _vatRate;
        decimal WithVat(decimal x) => Math.Round(x * (1 + vat / 100m), 2);

        var lines = new List<QuoteLine>(spec.Items.Count);
        var now = DateTime.UtcNow;
        foreach (var item in spec.Items)
        {
            var bind = binds[item.Id];
            products.TryGetValue(bind.ProductId ?? Guid.Empty, out var pr);
            var name = pr.Name ?? item.RawName;
            var sku = pr.Sku ?? item.RawSku;

            IReadOnlyList<Candidate> cands =
                bind.ProductId is { } id && offersByProduct.TryGetValue(id, out var list)
                    ? (onlyInStock ? list.Where(c => c.StockQuantity >= item.Quantity).ToList() : list)
                    : [];

            Candidate? overrideCand = overrides.TryGetValue(item.Id, out var overrideVendor)
                ? cands.FirstOrDefault(c => c.VendorId == overrideVendor)
                : null;
            var isOverride = overrideCand is not null;
            var (chosen, mlReason) = overrideCand is not null
                ? (overrideCand, (string?)null)
                : Select(cands, strategy, weightPrice, weightLeadTime, item.Quantity, bind.Score, now);
            if (chosen is null)
            {
                lines.Add(new QuoteLine(item.Id, bind.ProductId, sku, name, item.Quantity,
                    false, null, null, 0m, 0m, 0, 0, cands.Count,
                    DiscountPercent: 0m, UnitPriceDiscounted: 0m, UnitPriceWithVat: 0m,
                    UnitPriceWithVatDiscounted: 0m, LineTotalWithVat: 0m,
                    Manufacturer: pr.Manufacturer,
                    SelectionReason: bind.ProductId is null ? "Не найдено в каталоге (ML)" : "Нет предложений поставщиков",
                    MatchKind: bind.Kind.ToString(), MatchScore: bind.Score));
            }
            else
            {
                var lineTotal = chosen.EffectivePrice * item.Quantity;
                var reason = isOverride ? "Выбрано вручную"
                    : mlReason ?? ReasonFor(chosen, cands, strategy, weightPrice, weightLeadTime);
                if (bind.Kind is MatchKind.Analog or MatchKind.FuzzyName && bind.Reason is not null)
                    reason = bind.Reason + " · " + reason;
                lines.Add(new QuoteLine(item.Id, bind.ProductId, sku, name, item.Quantity,
                    true, chosen.VendorId, chosen.VendorName,
                    UnitPrice: chosen.Price, LineTotal: lineTotal,
                    LeadTimeDays: chosen.LeadTimeDays, StockQuantity: chosen.StockQuantity, CandidateOffers: cands.Count,
                    DiscountPercent: chosen.DiscountPercent,
                    UnitPriceDiscounted: chosen.EffectivePrice,
                    UnitPriceWithVat: WithVat(chosen.Price),
                    UnitPriceWithVatDiscounted: WithVat(chosen.EffectivePrice),
                    LineTotalWithVat: WithVat(lineTotal),
                    Manufacturer: pr.Manufacturer,
                    SelectionReason: reason,
                    MatchKind: bind.Kind.ToString(),
                    MatchScore: bind.Score));
            }
        }

        var matched = lines.Where(l => l.Matched).ToList();
        var total = matched.Sum(l => l.LineTotal);
        var totalWithVat = matched.Sum(l => l.LineTotalWithVat);

        activity?.SetTag("quote.matched_positions", matched.Count);
        activity?.SetTag("quote.unmatched_positions", lines.Count - matched.Count);
        activity?.SetTag("quote.total_cost", (double)total);

        return new Quote(
            spec.Id, spec.Title, strategy, weightPrice, weightLeadTime, lines,
            TotalCost: total,
            MaxLeadTimeDays: matched.Count > 0 ? matched.Max(l => l.LeadTimeDays) : 0,
            VendorsUsed: matched.Select(l => l.VendorId).Distinct().Count(),
            MatchedPositions: matched.Count,
            UnmatchedPositions: lines.Count - matched.Count,
            VatRate: vat,
            TotalCostWithVat: totalWithVat);
    }

    public async Task<IReadOnlyList<Quote>> GenerateAllAsync(Guid specificationId,
        double weightPrice = 0.5, double weightLeadTime = 0.5, bool onlyInStock = false,
        CancellationToken ct = default)
    {
        var result = new List<Quote>(4);
        foreach (var s in new[] { QuoteStrategy.MinCost, QuoteStrategy.MinLeadTime, QuoteStrategy.Balanced, QuoteStrategy.MlRelevance })
            result.Add(await GenerateAsync(specificationId, s, weightPrice, weightLeadTime, onlyInStock, ct));
        return result;
    }

    private static (Candidate? Chosen, string? MlReason) Select(
        IReadOnlyList<Candidate> cands, QuoteStrategy strategy, double wPrice, double wLead,
        int quantity, double matchP, DateTime nowUtc)
    {
        if (cands.Count == 0) return (null, null);
        if (strategy == QuoteStrategy.MlRelevance)
        {
            var snaps = cands.Select(c => new OfferSnapshot(c.VendorId, c.VendorName, c.Price, c.EffectivePrice,
                c.LeadTimeDays, c.StockQuantity, c.DiscountPercent, c.LastPriceAtUtc)).ToList();
            var ranked = OfferRanker.Pick(snaps, quantity, matchP, nowUtc);
            if (ranked is null) return (null, null);
            var chosen = cands.First(c => c.VendorId == ranked.Value.Offer.VendorId
                                          && c.EffectivePrice == ranked.Value.Offer.EffectivePrice);
            return (chosen, ranked.Value.Reason);
        }
        var pick = strategy switch
        {
            QuoteStrategy.MinCost => cands.OrderBy(c => c.EffectivePrice).ThenBy(c => c.LeadTimeDays).First(),
            QuoteStrategy.MinLeadTime => cands.OrderBy(c => c.LeadTimeDays).ThenBy(c => c.EffectivePrice).First(),
            _ => SelectBalanced(cands, wPrice, wLead)
        };
        return (pick, null);
    }

    private static Candidate SelectBalanced(IReadOnlyList<Candidate> cands, double wPrice, double wLead) =>
        cands.MinBy(c => BalancedScore(c, cands, wPrice, wLead))!;

    private static double BalancedScore(Candidate c, IReadOnlyList<Candidate> cands, double wPrice, double wLead)
    {
        var sum = wPrice + wLead;
        if (sum <= 0) { wPrice = wLead = 0.5; sum = 1; }
        wPrice /= sum; wLead /= sum;

        decimal minP = cands.Min(x => x.EffectivePrice), maxP = cands.Max(x => x.EffectivePrice);
        int minL = cands.Min(x => x.LeadTimeDays), maxL = cands.Max(x => x.LeadTimeDays);
        double pSpan = (double)(maxP - minP), lSpan = maxL - minL;
        double nP = pSpan > 0 ? (double)(c.EffectivePrice - minP) / pSpan : 0;
        double nL = lSpan > 0 ? (c.LeadTimeDays - minL) / lSpan : 0;
        return wPrice * nP + wLead * nL;
    }

    private static string ReasonFor(Candidate chosen, IReadOnlyList<Candidate> cands,
        QuoteStrategy strategy, double wPrice, double wLead) => strategy switch
    {
        QuoteStrategy.MinCost => chosen.DiscountPercent > 0
            ? $"Минимальная цена со скидкой: {chosen.EffectivePrice:0.##} ₽ (скидка {chosen.DiscountPercent:0.##}%)"
            : $"Минимальная цена: {chosen.EffectivePrice:0.##} ₽",
        QuoteStrategy.MinLeadTime => $"Минимальный срок поставки: {chosen.LeadTimeDays} дн",
        QuoteStrategy.Balanced => $"Баланс цена/срок: score {BalancedScore(chosen, cands, wPrice, wLead):F2}",
        QuoteStrategy.MlRelevance => "ML-актуальность",
        _ => "Выбран поставщик"
    };
}
