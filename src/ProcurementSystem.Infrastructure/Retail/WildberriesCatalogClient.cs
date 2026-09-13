using System.Text.Json;

namespace ProcurementSystem.Infrastructure.Retail;

/// <summary>
/// Поиск WB по внутреннему JSON витрины (цена в копейках). Без ключа, публичный search.wb.ru.
/// </summary>
public sealed class WildberriesCatalogClient(RetailHttpClient http)
{
    public const string Host = "wildberries.ru";

    public async Task<IReadOnlyList<RetailHitDto>> SearchAsync(string query, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 16);
        var versions = new[] { "v18", "v13", "v5" };
        using var race = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = versions.Select(ver => FetchVersion(ver, query, take, race.Token)).ToList();
        try
        {
            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);
                IReadOnlyList<RetailHitDto> parsed;
                try { parsed = await done; }
                catch { continue; }
                if (parsed.Count == 0) continue;
                race.Cancel();
                return parsed;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        return [];
    }

    async Task<IReadOnlyList<RetailHitDto>> FetchVersion(string ver, string query, int take, CancellationToken ct)
    {
        var url =
            $"https://search.wb.ru/exactmatch/ru/common/{ver}/search" +
            "?appType=1&curr=rub&dest=-1257786&lang=ru&page=1&resultset=catalog&sort=popular&spp=30" +
            "&query=" + Uri.EscapeDataString(query);
        var (_, body, _) = await http.GetAsync(url, ct);
        if (string.IsNullOrWhiteSpace(body)) return [];
        return Parse(body, take);
    }

    internal static IReadOnlyList<RetailHitDto> Parse(string json, int take)
    {
        take = Math.Clamp(take, 1, 16);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement products;
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("products", out products)) { }
        else if (!root.TryGetProperty("products", out products)) return [];
        if (products.ValueKind != JsonValueKind.Array) return [];

        var list = new List<RetailHitDto>();
        foreach (var p in products.EnumerateArray())
        {
            if (list.Count >= take) break;
            var id = p.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
            var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
            var brand = p.TryGetProperty("brand", out var b) ? b.GetString() : null;
            var price = ReadKopecks(p);
            if (price is null or <= 0) continue;
            var qty = 0;
            if (p.TryGetProperty("totalQuantity", out var tq) && tq.TryGetInt32(out var q1)) qty = q1;
            else if (p.TryGetProperty("volume", out var vol) && vol.TryGetInt32(out var q2)) qty = q2;
            var page = $"https://www.wildberries.ru/catalog/{id}/detail.aspx";
            list.Add(new RetailHitDto(
                "Wildberries", Host, name, brand, "WB-" + id,
                decimal.Round(price.Value, 2), "RUB", qty > 0, page, "Wildberries"));
        }
        return list;
    }

    static decimal? ReadKopecks(JsonElement p)
    {
        if (p.TryGetProperty("salePriceU", out var sale) && sale.TryGetInt64(out var s) && s > 0)
            return s / 100m;
        if (p.TryGetProperty("priceU", out var price) && price.TryGetInt64(out var pr) && pr > 0)
            return pr / 100m;
        if (p.TryGetProperty("sizes", out var sizes) && sizes.ValueKind == JsonValueKind.Array)
        {
            foreach (var sz in sizes.EnumerateArray())
            {
                if (sz.TryGetProperty("price", out var po) && po.ValueKind == JsonValueKind.Object)
                {
                    if (po.TryGetProperty("product", out var prod) && prod.TryGetInt64(out var pp) && pp > 0)
                        return pp / 100m;
                    if (po.TryGetProperty("total", out var tot) && tot.TryGetInt64(out var tt) && tt > 0)
                        return tt / 100m;
                }
            }
        }
        return null;
    }
}
