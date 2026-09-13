using System.Net;
using System.Text;

namespace ProcurementSystem.Infrastructure.Retail;

/// <summary>HTTP к публичным витринам: таймаут, лимит тела, без обхода капчи/логина.</summary>
public sealed class RetailHttpClient(HttpClient http)
{
    public const int MaxBytes = 1_500_000;
    static readonly SemaphoreSlim Gate = new(8);

    public async Task<(int Status, string? Body, string? Url)> GetAsync(string url, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await SendAsync(url, ct);
        }
        finally { Gate.Release(); }
    }

    async Task<(int Status, string? Body, string? Url)> SendAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "text/html,application/json;q=0.9,*/*;q=0.8");
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var final = resp.RequestMessage?.RequestUri?.ToString() ?? url;
        if ((int)resp.StatusCode is 401 or 403 or 429 or >= 500)
            return ((int)resp.StatusCode, null, final);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buf = new byte[8192];
        var total = 0;
        while (total < MaxBytes)
        {
            var n = await stream.ReadAsync(buf.AsMemory(0, Math.Min(buf.Length, MaxBytes - total)), ct);
            if (n == 0) break;
            ms.Write(buf, 0, n);
            total += n;
        }
        var charset = resp.Content.Headers.ContentType?.CharSet;
        Encoding enc;
        try { enc = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset); }
        catch { enc = Encoding.UTF8; }
        return ((int)resp.StatusCode, enc.GetString(ms.ToArray()), final);
    }
}

public static class RetailHttp
{
    public static HttpClient Create(HttpMessageHandler? handler = null)
    {
        var h = handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 4,
            AutomaticDecompression = DecompressionMethods.All,
        };
        var http = handler is null ? new HttpClient(h, disposeHandler: true) : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(10);
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (compatible; ProcurementSystem/1.0; +coursework; Chelyabinsk State University)");
        return http;
    }
}
