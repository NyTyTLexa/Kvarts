namespace ProcurementSystem.Services.Matching.Matching;

internal readonly record struct OfferSnapshot(
    Guid VendorId,
    string VendorName,
    decimal Price,
    decimal EffectivePrice,
    int LeadTimeDays,
    int StockQuantity,
    decimal DiscountPercent,
    DateTime LastPriceAtUtc);

internal readonly record struct RankedOffer(
    OfferSnapshot Offer,
    double Score,
    double Freshness,
    double PriceAdvantage,
    double LeadAdvantage,
    double StockCoverage,
    string Reason);

/// <summary>
/// Ранжирование офферов «по актуальности»: свежая цена, покрытие остатком,
/// относительная цена и срок. Веса фиксированы и интерпретируемы на защите.
/// </summary>
internal static class OfferRanker
{
    public const double WMatch = 0.28;
    public const double WFresh = 0.22;
    public const double WStock = 0.18;
    public const double WPrice = 0.20;
    public const double WLead = 0.12;
    public static readonly TimeSpan FreshHalfLife = TimeSpan.FromDays(45);

    public static RankedOffer? Pick(IReadOnlyList<OfferSnapshot> offers, int quantity, double matchProbability, DateTime nowUtc)
    {
        if (offers.Count == 0) return null;
        RankedOffer? best = null;
        foreach (var o in offers)
        {
            var r = Score(o, offers, quantity, matchProbability, nowUtc);
            if (best is null || r.Score > best.Value.Score) best = r;
        }
        return best;
    }

    public static RankedOffer Score(OfferSnapshot o, IReadOnlyList<OfferSnapshot> all, int quantity, double matchProbability, DateTime nowUtc)
    {
        var minP = all.Min(x => x.EffectivePrice);
        var maxP = all.Max(x => x.EffectivePrice);
        var minL = all.Min(x => x.LeadTimeDays);
        var maxL = all.Max(x => x.LeadTimeDays);
        var pSpan = (double)(maxP - minP);
        var lSpan = maxL - minL;
        var priceAdv = pSpan > 0 ? 1.0 - (double)(o.EffectivePrice - minP) / pSpan : 1.0;
        var leadAdv = lSpan > 0 ? 1.0 - (o.LeadTimeDays - minL) / lSpan : 1.0;
        var ageDays = Math.Max(0, (nowUtc - o.LastPriceAtUtc).TotalDays);
        var freshness = Math.Exp(-Math.Log(2) * ageDays / FreshHalfLife.TotalDays);
        var stockCov = quantity <= 0 ? 1 : Math.Clamp(o.StockQuantity / (double)quantity, 0, 1);

        var score = WMatch * matchProbability
                    + WFresh * freshness
                    + WStock * stockCov
                    + WPrice * priceAdv
                    + WLead * leadAdv;

        var bits = new List<string>(4);
        if (matchProbability >= 0.8) bits.Add($"сходство {matchProbability:0.00}");
        else bits.Add($"P={matchProbability:0.00}");
        bits.Add(freshness >= 0.7 ? $"цена свежая ({ageDays:0} дн.)" : $"цена давняя ({ageDays:0} дн.)");
        if (stockCov >= 1) bits.Add("в наличии");
        else if (stockCov > 0) bits.Add($"остаток {o.StockQuantity} из {quantity}");
        else bits.Add("нет на складе");
        if (o.DiscountPercent > 0) bits.Add($"скидка {o.DiscountPercent:0.#}%");

        return new RankedOffer(o, score, freshness, priceAdv, leadAdv, stockCov,
            "ML-актуальность: " + string.Join(", ", bits) + $", score {score:0.00}");
    }
}
