using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProcurementSystem.Services.Retail.Retail;

/// <summary>
/// Универсальный съём карточки: JSON-LD Product, OpenGraph, microdata.
/// Один раз написанный разбор закрывает большинство магазинов без отдельного парсера.
/// </summary>
public static class StructuredDataExtractor
{
    static readonly Regex LdJson = new(
        @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    static readonly Regex Meta = new(
        @"<meta\s+[^>]*(?:property|name)\s*=\s*[""'](?<k>[^""']+)[""'][^>]*content\s*=\s*[""'](?<v>[^""']*)[""'][^>]*>|<meta\s+[^>]*content\s*=\s*[""'](?<v2>[^""']*)[""'][^>]*(?:property|name)\s*=\s*[""'](?<k2>[^""']+)[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex TitleTag = new(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static RetailHitDto? Extract(string html, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(html) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
            return null;
        if (!ShopDirectory.IsCandidateUrl(uri)) return null;

        foreach (Match m in LdJson.Matches(html))
        {
            var json = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            if (json.Length < 10) continue;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var hit = FromJsonLd(doc.RootElement, uri);
                if (hit is not null) return hit;
            }
            catch (JsonException) { /* битый ld+json — идём дальше */ }
        }

        return FromMeta(html, uri);
    }

    public static IReadOnlyList<string> ExtractSameHostLinks(string html, string pageUrl, int take = 8)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page)) return [];
        var set = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(html, @"href\s*=\s*[""'](?<u>[^""'#]+)[""']", RegexOptions.IgnoreCase))
        {
            var raw = System.Net.WebUtility.HtmlDecode(m.Groups["u"].Value);
            if (!TryAbsolute(page, raw, out var link)) continue;
            if (!string.Equals(ShopDirectory.NormalizeHost(link.Host), ShopDirectory.NormalizeHost(page.Host), StringComparison.OrdinalIgnoreCase))
                continue;
            if (!ShopDirectory.IsCandidateUrl(link) || !ShopDirectory.LooksLikeProductUrl(link)) continue;
            if (string.Equals(link.GetLeftPart(UriPartial.Path), page.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase))
                continue;
            var canon = link.GetLeftPart(UriPartial.Path);
            if (!seen.Add(canon)) continue;
            set.Add(link.ToString());
            if (set.Count >= take) break;
        }
        return set;
    }

    static RetailHitDto? FromJsonLd(JsonElement el, Uri page)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in el.EnumerateArray())
            {
                var hit = FromJsonLd(x, page);
                if (hit is not null) return hit;
            }
            return null;
        }
        if (el.ValueKind != JsonValueKind.Object) return null;

        if (el.TryGetProperty("@graph", out var graph))
            return FromJsonLd(graph, page);

        if (!IsType(el, "Product") && !IsType(el, "Offer"))
            return null;

        JsonElement offers = default;
        var hasOffers = el.TryGetProperty("offers", out offers);
        if (IsType(el, "Offer") && !hasOffers) offers = el;

        var price = hasOffers || IsType(el, "Offer") ? ReadPrice(offers) : ReadDecimal(el, "price");
        if (price is null or <= 0) return null;

        var name = ReadString(el, "name") ?? ReadString(el, "title");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var brand = ReadBrand(el);
        var sku = ReadString(el, "sku") ?? ReadString(el, "mpn") ?? ReadString(el, "gtin13") ?? ReadString(el, "gtin");
        var currency = ReadCurrency(hasOffers || IsType(el, "Offer") ? offers : el) ?? "RUB";
        var inStock = ReadAvailability(hasOffers || IsType(el, "Offer") ? offers : el);
        var url = ReadString(el, "url");
        if (string.IsNullOrWhiteSpace(url)) url = page.ToString();
        else if (TryAbsolute(page, url, out var abs)) url = abs.ToString();

        var host = ShopDirectory.NormalizeHost(page.Host);
        return new RetailHitDto(
            ShopDirectory.DisplayName(host), host, name.Trim(), brand, sku?.Trim(),
            decimal.Round(price.Value, 2), currency.ToUpperInvariant(), inStock, url!, "JsonLd");
    }

    static RetailHitDto? FromMeta(string html, Uri page)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Meta.Matches(html))
        {
            var k = m.Groups["k"].Success ? m.Groups["k"].Value : m.Groups["k2"].Value;
            var v = m.Groups["v"].Success ? m.Groups["v"].Value : m.Groups["v2"].Value;
            if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v))
                map.TryAdd(k.Trim(), System.Net.WebUtility.HtmlDecode(v).Trim());
        }

        map.TryGetValue("og:title", out var title);
        map.TryGetValue("product:price:amount", out var p1);
        map.TryGetValue("og:price:amount", out var p2);
        map.TryGetValue("product:price:currency", out var cur);
        map.TryGetValue("og:url", out var ogUrl);
        if (title is null)
        {
            var tm = TitleTag.Match(html);
            if (tm.Success) title = System.Net.WebUtility.HtmlDecode(tm.Groups[1].Value).Trim();
        }
        var price = ParseDecimal(p1) ?? ParseDecimal(p2);
        if (string.IsNullOrWhiteSpace(title) || price is null or <= 0) return null;

        var host = ShopDirectory.NormalizeHost(page.Host);
        var url = ogUrl is not null && TryAbsolute(page, ogUrl, out var abs) ? abs.ToString() : page.ToString();
        return new RetailHitDto(
            ShopDirectory.DisplayName(host), host, title, null, null,
            decimal.Round(price.Value, 2), (cur ?? "RUB").ToUpperInvariant(), true, url, "OpenGraph");
    }

    static bool IsType(JsonElement el, string type)
    {
        if (!el.TryGetProperty("@type", out var t)) return false;
        if (t.ValueKind == JsonValueKind.String && t.GetString() is { } s)
            return s.Equals(type, StringComparison.OrdinalIgnoreCase) ||
                   s.EndsWith("/" + type, StringComparison.OrdinalIgnoreCase);
        if (t.ValueKind == JsonValueKind.Array)
            foreach (var x in t.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String &&
                    (x.GetString()!.Equals(type, StringComparison.OrdinalIgnoreCase) ||
                     x.GetString()!.EndsWith("/" + type, StringComparison.OrdinalIgnoreCase)))
                    return true;
        return false;
    }

    static decimal? ReadPrice(JsonElement offers)
    {
        if (offers.ValueKind == JsonValueKind.Array)
        {
            foreach (var o in offers.EnumerateArray())
            {
                var p = ReadPrice(o);
                if (p is > 0) return p;
            }
            return null;
        }
        if (offers.ValueKind != JsonValueKind.Object) return ParseDecimal(offers.ToString());
        return ReadDecimal(offers, "price")
            ?? ReadDecimal(offers, "lowPrice")
            ?? ReadDecimal(offers, "highPrice");
    }

    static string? ReadCurrency(JsonElement offers)
    {
        if (offers.ValueKind == JsonValueKind.Array && offers.GetArrayLength() > 0)
            return ReadCurrency(offers[0]);
        return ReadString(offers, "priceCurrency") ?? ReadString(offers, "currency");
    }

    static bool ReadAvailability(JsonElement offers)
    {
        if (offers.ValueKind == JsonValueKind.Array && offers.GetArrayLength() > 0)
            return ReadAvailability(offers[0]);
        var a = ReadString(offers, "availability") ?? "";
        if (a.Contains("OutOfStock", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    static string? ReadBrand(JsonElement el)
    {
        if (!el.TryGetProperty("brand", out var b)) return ReadString(el, "manufacturer");
        if (b.ValueKind == JsonValueKind.String) return b.GetString();
        if (b.ValueKind == JsonValueKind.Object) return ReadString(b, "name");
        return null;
    }

    static string? ReadString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            JsonValueKind.Object when v.TryGetProperty("name", out var n) => n.GetString(),
            _ => null
        };
    }

    static decimal? ReadDecimal(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return ParseDecimal(v.ToString());
    }

    internal static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Replace("\u00A0", "").Replace(" ", "").Replace("₽", "").Replace("RUB", "", StringComparison.OrdinalIgnoreCase);
        if (raw.Contains(',') && !raw.Contains('.')) raw = raw.Replace(',', '.');
        else raw = raw.Replace(",", "");
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    internal static bool TryAbsolute(Uri page, string raw, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (raw.StartsWith("//")) raw = page.Scheme + ":" + raw;
        return Uri.TryCreate(page, raw, out uri!) && uri.Scheme is "http" or "https";
    }
}
