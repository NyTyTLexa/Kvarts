using System.Globalization;
using System.Text;

namespace ProcurementSystem.Services.Matching.Matching;

/// <summary>
/// Токенизация и расстояние для русско-английских наименований номенклатуры.
/// Символьные 3-граммы ловят опечатки; словесные токены — синонимичные перестановки.
/// </summary>
internal static class TextFeatures
{
    private static readonly HashSet<string> Stop =
    [
        "и", "в", "во", "на", "с", "со", "для", "по", "от", "из", "к", "ко", "о", "об",
        "the", "of", "and", "or", "a", "an", "шт", "pcs", "мм", "cm"
    ];

    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var form = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static IReadOnlyList<string> Tokens(string? s)
    {
        var n = Normalize(s);
        if (n.Length == 0) return [];
        var words = n.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2 && !Stop.Contains(w))
            .ToList();
        var grams = new List<string>(n.Length);
        var compact = n.Replace(" ", "");
        if (compact.Length >= 3)
        {
            for (var i = 0; i <= compact.Length - 3; i++)
                grams.Add("#" + compact.Substring(i, 3));
        }
        words.AddRange(grams);
        return words;
    }

    public static double Jaccard(string? a, string? b)
    {
        var ta = Tokens(a).Where(t => t.Length >= 2 && t[0] != '#').ToHashSet();
        var tb = Tokens(b).Where(t => t.Length >= 2 && t[0] != '#').ToHashSet();
        if (ta.Count == 0 || tb.Count == 0) return 0;
        var inter = ta.Intersect(tb).Count();
        var union = ta.Count + tb.Count - inter;
        return union == 0 ? 0 : (double)inter / union;
    }

    public static double NormalizedLevenshtein(string? a, string? b)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (a.Length == 0 && b.Length == 0) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;
        var d = Levenshtein(a, b);
        return 1.0 - (double)d / Math.Max(a.Length, b.Length);
    }

    public static string Typo(string name, Random rng)
    {
        var n = Normalize(name);
        if (n.Length < 4) return n + "x";
        var chars = n.ToCharArray();
        var i = rng.Next(0, chars.Length - 1);
        (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
        return new string(chars);
    }

    private static int Levenshtein(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (var j = 0; j <= m; j++) prev[j] = j;
        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }
}
