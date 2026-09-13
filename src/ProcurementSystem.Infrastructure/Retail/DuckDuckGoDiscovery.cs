using System.Text.RegularExpressions;

namespace ProcurementSystem.Infrastructure.Retail;

/// <summary>
/// Поиск магазинов через публичную HTML-выдачу DuckDuckGo: запрос «{товар} купить»,
/// ссылки — кандидаты на карточку. Капчу и логин не обходим.
/// </summary>
public sealed class DuckDuckGoDiscovery(RetailHttpClient http)
{
    static readonly Regex ResultA = new(
        @"class\s*=\s*[""'][^""']*result__a[^""']*[""'][^>]*href\s*=\s*[""'](?<u>[^""']+)[""']|href\s*=\s*[""'](?<u2>[^""']+)[""'][^>]*class\s*=\s*[""'][^""']*result__a",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex AnyHttp = new(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<IReadOnlyList<Uri>> SearchAsync(string query, int take, CancellationToken ct)
    {
        var q = query.Trim() + " купить";
        var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(q);
        var (status, body, _) = await http.GetAsync(url, ct);
        if (status != 200 || string.IsNullOrWhiteSpace(body)) return [];
        return Parse(body, take);
    }

    internal static IReadOnlyList<Uri> Parse(string html, int take)
    {
        take = Math.Clamp(take, 1, 24);
        var list = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in ResultA.Matches(html))
        {
            var raw = m.Groups["u"].Success ? m.Groups["u"].Value : m.Groups["u2"].Value;
            if (TryUnwrap(raw, out var uri) && seen.Add(uri.GetLeftPart(UriPartial.Path)))
            {
                if (!ShopDirectory.IsCandidateUrl(uri)) continue;
                list.Add(uri);
                if (list.Count >= take) return list;
            }
        }

        if (list.Count == 0)
        {
            foreach (Match m in AnyHttp.Matches(html))
            {
                if (!TryUnwrap(m.Value.TrimEnd(')', ',', '.'), out var uri)) continue;
                if (uri.Host.Contains("duckduckgo", StringComparison.OrdinalIgnoreCase)) continue;
                if (!ShopDirectory.IsCandidateUrl(uri)) continue;
                if (!seen.Add(uri.GetLeftPart(UriPartial.Path))) continue;
                list.Add(uri);
                if (list.Count >= take) break;
            }
        }
        return list;
    }

    internal static bool TryUnwrap(string raw, out Uri uri)
    {
        uri = null!;
        raw = System.Net.WebUtility.HtmlDecode(raw);
        if (raw.StartsWith("//")) raw = "https:" + raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var u)) return false;

        if (u.Host.Contains("duckduckgo", StringComparison.OrdinalIgnoreCase))
        {
            var uddg = QueryParam(u, "uddg") ?? QueryParam(u, "u");
            if (string.IsNullOrWhiteSpace(uddg)) return false;
            raw = Uri.UnescapeDataString(uddg.Replace('+', ' '));
            if (!Uri.TryCreate(raw, UriKind.Absolute, out u)) return false;
        }
        uri = u;
        return u.Scheme is "http" or "https";
    }

    static string? QueryParam(Uri u, string name)
    {
        var q = u.Query.TrimStart('?');
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0) continue;
            if (part[..i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return part[(i + 1)..];
        }
        return null;
    }
}
