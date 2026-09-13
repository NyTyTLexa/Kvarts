using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Retail;
using ProcurementSystem.Services.Retail.Persistence;

namespace ProcurementSystem.Services.Retail.Retail;

public interface IRetailSearchService
{
    Task<RetailSearchResult> SearchAsync(RetailSearchRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<RetailShopDto>> ListShopsAsync(CancellationToken ct = default);
}

/// <summary>
/// Оркестратор витрин. HTTP к WB, выдаче и карточкам идёт пачками (WhenAll),
/// DbContext трогаем только после сети. Пишем в схему retail, не в public.
/// </summary>
public sealed class RetailSearchService(
    RetailHttpClient http,
    WildberriesCatalogClient wb,
    DuckDuckGoDiscovery ddg,
    RetailDbContext db) : IRetailSearchService
{
    public async Task<RetailSearchResult> SearchAsync(RetailSearchRequest req, CancellationToken ct = default)
    {
        var q = (req.Q ?? "").Trim();
        if (q.Length < 2 && string.IsNullOrWhiteSpace(req.Url))
            return new RetailSearchResult("", [], await ListShopsAsync(ct), ["Укажите запрос от 2 символов или URL карточки."]);

        var limit = Math.Clamp(req.Limit, 4, 24);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(28));
        var token = timeout.Token;

        var jobs = new List<Task<Batch>>();
        if (!string.IsNullOrWhiteSpace(req.Url))
            jobs.Add(RunProbe(req.Url.Trim(), token));
        if (req.Wildberries && q.Length >= 2)
            jobs.Add(RunWb(q, limit, token));
        if (req.Discover && q.Length >= 2)
            jobs.Add(RunDiscover(q, token));
        if (req.KnownShops && q.Length >= 2)
            jobs.Add(RunKnown(q, token));

        var hits = new List<RetailHitDto>();
        var errors = new List<string>();
        var shopNotes = new List<(string Host, string Name, string Kind, string? Template, bool Ok, string? Err)>();

        try
        {
            var parts = await Task.WhenAll(jobs);
            foreach (var p in parts)
            {
                hits.AddRange(p.Hits);
                errors.AddRange(p.Errors);
                shopNotes.AddRange(p.Notes);
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var j in jobs.Where(t => t.IsCompletedSuccessfully))
            {
                hits.AddRange(j.Result.Hits);
                errors.AddRange(j.Result.Errors);
                shopNotes.AddRange(j.Result.Notes);
            }
            errors.Add("Поиск оборван по таймауту — показаны уже найденные карточки.");
        }

        var unique = Dedup(hits).Take(limit).ToList();
        foreach (var h in unique)
            shopNotes.Add((h.Host, h.Shop, ShopDirectory.IsKnown(h.Host) ? "Known" : "Discovered", null, true, null));

        await PersistShops(shopNotes, ct);
        return new RetailSearchResult(q, unique, await ListShopsAsync(ct), errors.Distinct().Take(24).ToList());
    }

    public async Task<IReadOnlyList<RetailShopDto>> ListShopsAsync(CancellationToken ct = default)
    {
        await SeedKnownAsync(ct);
        return await db.RetailShops.AsNoTracking()
            .OrderByDescending(s => s.Kind == "Known")
            .ThenByDescending(s => s.HitCount)
            .ThenBy(s => s.DisplayName)
            .Select(s => new RetailShopDto(s.Host, s.DisplayName, s.Kind, s.LastSuccessUtc, s.HitCount, s.LastError))
            .ToListAsync(ct);
    }

    async Task<Batch> RunProbe(string url, CancellationToken ct)
    {
        var page = await FetchProductAsync(url, ct);
        if (page.Hit is not null) return new Batch([page.Hit], [], []);
        return new Batch([], [page.Error ?? "По URL не удалось снять карточку (нет JSON-LD/цены или сайт закрыт)."], []);
    }

    async Task<Batch> RunWb(string q, int limit, CancellationToken ct)
    {
        try
        {
            var found = await wb.SearchAsync(q, Math.Min(8, limit), ct);
            var err = found.Count == 0 ? new[] { "Wildberries: пусто или витрина недоступна." } : Array.Empty<string>();
            return new Batch(found, err, []);
        }
        catch (OperationCanceledException) { return Batch.Empty; }
        catch (Exception ex) { return new Batch([], [$"Wildberries: {Short(ex)}"], []); }
    }

    async Task<Batch> RunDiscover(string q, CancellationToken ct)
    {
        try
        {
            var links = await ddg.SearchAsync(q, 16, ct);
            if (links.Count == 0)
                return new Batch([], ["Поиск витрин: выдача пустая (DDG недоступен или ничего не нашёл)."], []);

            var pages = await Task.WhenAll(links.Take(12).Select(link => FetchProductOrFollowAsync(link.ToString(), ct)));
            var hits = pages.Select(p => p.Hit).OfType<RetailHitDto>().ToList();
            var errors = pages.Select(p => p.Error).Where(e => e is not null).Cast<string>().Take(6).ToList();
            return new Batch(hits, errors, []);
        }
        catch (OperationCanceledException) { return Batch.Empty; }
        catch (Exception ex) { return new Batch([], ["Поиск витрин: " + Short(ex)], []); }
    }

    async Task<Batch> RunKnown(string q, CancellationToken ct)
    {
        var shops = ShopDirectory.Known.Where(s => s.SearchUrlTemplate is not null).Take(8).ToList();
        var parts = await Task.WhenAll(shops.Select(shop => ProbeKnownShop(shop, q, ct)));
        return new Batch(
            parts.SelectMany(p => p.Hits).ToList(),
            parts.SelectMany(p => p.Errors).ToList(),
            parts.SelectMany(p => p.Notes).ToList());
    }

    async Task<Batch> ProbeKnownShop(KnownShop shop, string q, CancellationToken ct)
    {
        try
        {
            var searchUrl = ShopDirectory.SearchUrl(shop, q);
            var (status, body, final) = await http.GetAsync(searchUrl, ct);
            if (status != 200 || string.IsNullOrWhiteSpace(body) || final is null)
            {
                return new Batch([],
                    [$"{shop.DisplayName}: HTTP {status} — витрина закрыта или антибот."],
                    [(shop.Host, shop.DisplayName, "Known", shop.SearchUrlTemplate, false, $"HTTP {status}")]);
            }

            var hits = new List<RetailHitDto>();
            var direct = StructuredDataExtractor.Extract(body, final);
            if (direct is not null) hits.Add(direct);

            var productLinks = StructuredDataExtractor.ExtractSameHostLinks(body, final, 8);
            if (productLinks.Count > 0)
            {
                var extra = await Task.WhenAll(productLinks.Select(pl => FetchProductAsync(pl, ct)));
                hits.AddRange(extra.Select(x => x.Hit).OfType<RetailHitDto>());
            }

            return new Batch(hits, [], []);
        }
        catch (OperationCanceledException) { return Batch.Empty; }
        catch (Exception ex) { return new Batch([], [$"{shop.DisplayName}: {Short(ex)}"], []); }
    }

    async Task<(RetailHitDto? Hit, string? Error)> FetchProductAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !ShopDirectory.IsCandidateUrl(uri))
            return (null, "URL отклонён (не магазин или служебная страница).");
        try
        {
            var (status, body, final) = await http.GetAsync(url, ct);
            if (status != 200 || string.IsNullOrWhiteSpace(body) || final is null)
                return (null, $"{ShopDirectory.NormalizeHost(uri.Host)}: HTTP {status}");
            var hit = StructuredDataExtractor.Extract(body, final);
            if (hit is null) return (null, $"{ShopDirectory.NormalizeHost(new Uri(final).Host)}: нет Product/цены в разметке.");
            return (hit, null);
        }
        catch (OperationCanceledException) { return (null, null); }
        catch (Exception ex) { return (null, Short(ex)); }
    }

    async Task<(RetailHitDto? Hit, string? Error)> FetchProductOrFollowAsync(string url, CancellationToken ct)
    {
        var first = await FetchProductAsync(url, ct);
        if (first.Hit is not null) return first;
        try
        {
            var (status, body, final) = await http.GetAsync(url, ct);
            if (status != 200 || string.IsNullOrWhiteSpace(body) || final is null) return first;
            var links = StructuredDataExtractor.ExtractSameHostLinks(body, final, 1);
            if (links.Count == 0) return first;
            return await FetchProductAsync(links[0], ct);
        }
        catch { return first; }
    }

    async Task PersistShops(List<(string Host, string Name, string Kind, string? Template, bool Ok, string? Err)> notes, CancellationToken ct)
    {
        await SeedKnownAsync(ct);
        foreach (var n in notes)
        {
            var host = ShopDirectory.NormalizeHost(n.Host);
            var row = await db.RetailShops.FirstOrDefaultAsync(s => s.Host == host, ct);
            if (row is null)
            {
                row = new RetailShop
                {
                    Host = host,
                    DisplayName = n.Name,
                    Kind = n.Kind,
                    SearchUrlTemplate = n.Template,
                    FirstSeenUtc = DateTime.UtcNow,
                };
                db.RetailShops.Add(row);
            }
            if (n.Ok)
            {
                row.LastSuccessUtc = DateTime.UtcNow;
                row.LastError = null;
                row.HitCount++;
                if (row.Kind == "Discovered" && n.Kind == "Known") row.Kind = "Known";
            }
            else if (row.LastSuccessUtc is null)
                row.LastError = n.Err;
        }
        try { await db.SaveChangesAsync(ct); }
        catch { db.ChangeTracker.Clear(); }
    }

    async Task SeedKnownAsync(CancellationToken ct)
    {
        var existing = await db.RetailShops.Select(s => s.Host).ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var k in ShopDirectory.Known)
        {
            if (!set.Add(k.Host)) continue;
            db.RetailShops.Add(new RetailShop
            {
                Host = k.Host,
                DisplayName = k.DisplayName,
                Kind = "Known",
                SearchUrlTemplate = k.SearchUrlTemplate,
                FirstSeenUtc = DateTime.UtcNow,
            });
        }
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }

    static List<RetailHitDto> Dedup(List<RetailHitDto> hits)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<RetailHitDto>();
        foreach (var h in hits.OrderBy(x => x.Price))
        {
            var key = $"{h.Host}|{(h.Sku ?? h.Url)}";
            if (!seen.Add(key)) continue;
            list.Add(h);
        }
        return list;
    }

    static string Short(Exception ex)
    {
        var m = ex.InnerException?.Message ?? ex.Message;
        return m.Length > 160 ? m[..160] + "…" : m;
    }

    readonly record struct Batch(
        IReadOnlyList<RetailHitDto> Hits,
        IReadOnlyList<string> Errors,
        IReadOnlyList<(string, string, string, string?, bool, string?)> Notes)
    {
        public static Batch Empty => new([], [], []);
    }
}
