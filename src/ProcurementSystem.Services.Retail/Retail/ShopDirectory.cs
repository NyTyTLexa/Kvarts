using System.Text.RegularExpressions;

namespace ProcurementSystem.Services.Retail.Retail;

public sealed record KnownShop(string Host, string DisplayName, string? SearchUrlTemplate);

/// <summary>
/// Справочник витрин и фильтр «это магазин, а не соцсеть».
/// Поиск сам находит новые хосты; сюда они попадают, если страница отдала Product.
/// </summary>
public static class ShopDirectory
{
    public static readonly KnownShop[] Known =
    [
        new("wildberries.ru", "Wildberries", null),
        new("ozon.ru", "Ozon", "https://www.ozon.ru/search/?text={q}"),
        new("citilink.ru", "Ситилинк", "https://www.citilink.ru/search/?text={q}"),
        new("dns-shop.ru", "DNS", "https://www.dns-shop.ru/search/?q={q}"),
        new("market.yandex.ru", "Яндекс Маркет", "https://market.yandex.ru/search?text={q}"),
        new("mvideo.ru", "М.Видео", "https://www.mvideo.ru/product-list-page?q={q}"),
        new("eldorado.ru", "Эльдорадо", "https://www.eldorado.ru/search/catalog.php?q={q}"),
        new("regard.ru", "Регард", "https://www.regard.ru/catalog?query={q}"),
        new("onlinetrade.ru", "OnlineTrade", "https://www.onlinetrade.ru/search.html?query={q}"),
        new("nix.ru", "NIX", "https://www.nix.ru/search.html?text={q}"),
        new("pleer.ru", "Pleer.ru", "https://www.pleer.ru/search_{q}.html"),
        new("svyaznoy.ru", "Связной", "https://www.svyaznoy.ru/search?q={q}"),
        new("citilink.kz", "Ситилинк", null),
        new("vseinstrumenti.ru", "ВсеИнструменты", "https://www.vseinstrumenti.ru/search/?what={q}"),
        new("komus.ru", "Комус", "https://www.komus.ru/search/?q={q}"),
    ];

    static readonly string[] Blocked =
    [
        "youtube.com", "youtu.be", "vk.com", "vk.ru", "facebook.com", "instagram.com",
        "twitter.com", "x.com", "t.me", "telegram.org", "wikipedia.org", "reddit.com",
        "tiktok.com", "pinterest.com", "ok.ru", "dzen.ru", "livejournal.com",
        "github.com", "stackoverflow.com", "google.com", "google.ru", "bing.com",
        "duckduckgo.com", "yahoo.com", "mail.ru", "hh.ru", "avito.ru", "cian.ru",
        "youla.ru", "facebook.net", "yandex.ru", "ya.ru",
    ];

    static readonly Regex ProductPath = new(
        @"/(product|products|catalog|tovar|item|goods|detail|p|g|card)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string NormalizeHost(string host)
    {
        host = host.Trim().ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];
        return host;
    }

    public static bool IsBlocked(string host)
    {
        host = NormalizeHost(host);
        if (FindKnown(host) is not null) return false;
        return Blocked.Any(b => host == b || host.EndsWith('.' + b));
    }

    public static KnownShop? FindKnown(string host)
    {
        host = NormalizeHost(host);
        return Known.FirstOrDefault(k => host == k.Host || host.EndsWith('.' + k.Host));
    }

    public static bool IsKnown(string host) => FindKnown(host) is not null;

    public static string DisplayName(string host)
    {
        var known = FindKnown(host);
        if (known is not null) return known.DisplayName;
        host = NormalizeHost(host);
        var root = host.Split('.')[0];
        return char.ToUpper(root[0]) + root[1..];
    }

    public static bool LooksLikeProductUrl(Uri uri)
        => ProductPath.IsMatch(uri.AbsolutePath) || IsKnown(uri.Host);

    public static bool IsCandidateUrl(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https")) return false;
        if (IsBlocked(uri.Host)) return false;
        var path = uri.AbsolutePath.ToLowerInvariant();
        if (path.Contains("/login") || path.Contains("/cart") || path.Contains("/account") || path.Contains("/checkout"))
            return false;
        return true;
    }

    public static string SearchUrl(KnownShop shop, string query)
    {
        if (string.IsNullOrWhiteSpace(shop.SearchUrlTemplate)) return $"https://{shop.Host}/";
        return shop.SearchUrlTemplate.Replace("{q}", Uri.EscapeDataString(query), StringComparison.Ordinal);
    }
}
