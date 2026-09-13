using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Matching;
using ProcurementSystem.Infrastructure.Observability;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;

namespace ProcurementSystem.Infrastructure.Quoting;

/// <summary>Стратегия подбора офферов под спецификацию.</summary>
public enum QuoteStrategy
{
    MinCost,       // минимальная стоимость
    MinLeadTime,   // минимальный срок поставки
    Balanced,      // взвешенный баланс цена/срок
    MlRelevance    // ML: свежая цена + наличие + сходство (ТЗ п.5.3/5.5)
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
    decimal UnitPrice,                  // исходная цена без скидки, без НДС
    decimal LineTotal,                  // итог со скидкой, без НДС
    int LeadTimeDays,
    int StockQuantity,
    int CandidateOffers,
    decimal DiscountPercent,            // применённый % скидки
    decimal UnitPriceDiscounted,        // цена со скидкой, без НДС
    decimal UnitPriceWithVat,           // исходная цена с НДС
    decimal UnitPriceWithVatDiscounted, // цена со скидкой, с НДС
    decimal LineTotalWithVat,           // итог со скидкой, с НДС
    string? Manufacturer,               // производитель товара (ТЗ п.5.5)
    string? SelectionReason,            // причина выбора поставщика (ТЗ п.5.5)
    string? MatchKind = null,
    double? MatchScore = null);

/// <summary>Готовое коммерческое предложение по выбранной стратегии.</summary>
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
    decimal VatRate,             // ставка НДС, % (из конфига Quote:VatRate, по умолч. 20)
    decimal TotalCostWithVat);   // итог со скидкой, с НДС

public interface IQuoteGenerator
{
    /// <summary>Сгенерировать КП по одной стратегии.</summary>
    Task<Quote> GenerateAsync(Guid specificationId, QuoteStrategy strategy,
        double weightPrice = 0.5, double weightLeadTime = 0.5, bool onlyInStock = false,
        CancellationToken ct = default);

    /// <summary>Сгенерировать все варианты КП для сравнения (цена / срок / баланс / ML-актуальность).</summary>
    Task<IReadOnlyList<Quote>> GenerateAllAsync(Guid specificationId,
        double weightPrice = 0.5, double weightLeadTime = 0.5, bool onlyInStock = false,
        CancellationToken ct = default);
}

/// <summary>Разрешение позиции спецификации на товар каталога (в т.ч. ML-аналог).</summary>
file readonly record struct ItemBind(Guid? ProductId, MatchKind Kind, double Score, string? Reason);

/// <summary>Настройки генерации КП (ставка НДС и т.п.).</summary>
public class QuoteOptions
{
    public decimal VatRate { get; set; } = 20m;   // НДС, % (РФ — 20%)
}

/// <summary>
/// Ядро «изюминки» системы: по спецификации и каталогу офферов подбирает поставщиков
/// под выбранную стратегию и считает итоговые показатели КП (стоимость, срок, число поставщиков).
/// Скидки по вендору/производителю (ТЗ п.5.4) применяются автоматически: приоритет у скидки поставщика,
/// при её отсутствии — скидка производителя товара. Цена считается без НДС и с НДС независимо.
/// </summary>
public class QuoteGenerator(AppDbContext db, IDiscountService discounts, IOptions<QuoteOptions> opts, IRelevanceMatcher? matcher = null) : IQuoteGenerator
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

        // Товары с производителем — производитель нужен, чтобы применить скидку по производителю.
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Name, p.Manufacturer })
            .ToDictionaryAsync(p => p.Id, p => (p.Sku, p.Name, p.Manufacturer), ct);

        // Ручные корректировки поставщика по позициям спецификации (ТЗ п.5.5): фиксируют выбранного
        // вручную поставщика — он применяется при подборе вместо автоматической стратегии.
        var itemIds = spec.Items.Select(i => i.Id).ToList();
        var overrides = await db.QuoteOverrides.AsNoTracking()
            .Where(o => itemIds.Contains(o.SpecificationItemId))
            .ToDictionaryAsync(o => o.SpecificationItemId, o => o.VendorId, ct);

        // Все офферы по сопоставленным товарам одним запросом.
        var rawOffers = await db.PriceListItems.AsNoTracking()
            .Where(o => productIds.Contains(o.ProductId))
            .Select(o => new { o.ProductId, o.VendorId, VendorName = o.Vendor.Name, o.Price, o.LeadTimeDays, o.StockQuantity, o.UpdatedAtUtc, o.CreatedAtUtc })
            .ToListAsync(ct);

        var lastPriceAt = await db.PriceHistory.AsNoTracking()
            .Where(h => productIds.Contains(h.ProductId))
            .GroupBy(h => new { h.ProductId, h.VendorId })
            .Select(g => new { g.Key.ProductId, g.Key.VendorId, At = g.Max(x => x.RecordedAtUtc) })
            .ToListAsync(ct);
        var lastPriceMap = lastPriceAt.ToDictionary(x => (x.ProductId, x.VendorId), x => x.At);

        // Активные скидки по задействованным поставщикам/производителям (ТЗ п.5.4).
        var vendorIds = rawOffers.Select(o => o.VendorId).Distinct().ToList();
        var manufacturers = products.Values.Select(p => p.Manufacturer)
            .Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!).Distinct().ToList();
        var applicable = await discounts.GetActiveForAsync(vendorIds, manufacturers, ct);
        var vendorDisc = applicable.Where(a => a.VendorId is not null)
            .ToDictionary(a => a.VendorId!.Value, a => a.Percent);
        var mfrDisc = applicable.Where(a => a.Manufacturer is not null)
            .GroupBy(a => a.Manufacturer!).ToDictionary(g => g.Key, g => g.First().Percent);

        // Кандидаты по товару с посчитанной эффективной ценой: приоритет у скидки поставщика,
        // при её отсутствии — скидка производителя товара.
        var offersByProduct = rawOffers.GroupBy(o => o.ProductId).ToDictionary(
            g => g.Key,
            g =>
            {
                products.TryGetValue(g.Key, out var pr);
                var mfr = pr.Manufacturer;
                return (IReadOnlyList<Candidate>)g.Select(o =>
                {
                    decimal disc = vendorDisc.TryGetValue(o.VendorId, out var vd) ? vd
                        : (mfr is not null && mfrDisc.TryGetValue(mfr, out var md) ? md : 0m);
                    var eff = Math.Round(o.Price * (1 - disc / 100m), 2);
                    var at = lastPriceMap.TryGetValue((o.ProductId, o.VendorId), out var hist)
                        ? hist : (o.UpdatedAtUtc ?? o.CreatedAtUtc);
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
                var lineTotal = chosen.EffectivePrice * item.Quantity;   // со скидкой, без НДС
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

    // Стратегии оптимизируют по эффективной (со скидкой) цене — это реальная стоимость к оплате.
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

    /// <summary>
    /// Балансная стратегия: цену (со скидкой) и срок нормируем в [0..1] среди кандидатов на позицию
    /// и берём оффер с минимальным взвешенным score = wPrice·nPrice + wLead·nLead.
    /// </summary>
    private static Candidate SelectBalanced(IReadOnlyList<Candidate> cands, double wPrice, double wLead) =>
        cands.MinBy(c => BalancedScore(c, cands, wPrice, wLead))!;

    /// <summary>Взвешенный score кандидата в [0..1] для балансной стратегии (меньше — лучше).</summary>
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

    /// <summary>Текстовая причина выбора поставщика по стратегии (ТЗ п.5.5: «почему выбран именно этот»).</summary>
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
