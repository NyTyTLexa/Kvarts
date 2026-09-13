using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace ProcurementSystem.Services.Matching.Clients;

public interface ICatalogClient
{
    Task<int> CountProductsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogProductSnapshot>> ListAllProductsAsync(CancellationToken ct = default);
    Task<bool> ProductExistsAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Номенклатура чужая. Для скоринга нужен полный снимок: витрина Catalog режет pageSize=48,
/// на стенде ~10^5 SKU это тысячи HTTP. Если есть Postgres — читаем схему catalog только SELECT.
/// HTTP остаётся запасным путём и для ProductExists.
/// </summary>
public sealed class CatalogClient(HttpClient http, ILogger<CatalogClient> log, IConfiguration config) : ICatalogClient
{
    const int BrowsePageSize = 48;
    const int MaxBrowsePages = 5000;
    const int BrowseConcurrency = 8;
    const string SnapshotSql = """SELECT "Id", "Sku", "Name", "Manufacturer", "Category" FROM catalog."Products" """;
    const string CountSql = """SELECT COUNT(*) FROM catalog."Products" """;

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    readonly string? _cs = config.GetConnectionString("Postgres");

    public async Task<int> CountProductsAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cs))
        {
            try { return await CountFromSqlAsync(ct); }
            catch (Exception ex)
            {
                log.LogWarning(ex, "catalog.Products COUNT недоступен, считаем через HTTP");
            }
        }
        var page = await GetBrowsePageAsync(1, 1, ct);
        return page?.Total ?? 0;
    }

    public async Task<IReadOnlyList<CatalogProductSnapshot>> ListAllProductsAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cs))
        {
            try
            {
                var rows = await ListFromSqlAsync(ct);
                log.LogInformation("снимок каталога: {Count} строк, источник=sql", rows.Count);
                return rows;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "catalog.Products SELECT недоступен, читаем витрину HTTP");
            }
        }
        var httpRows = await ListFromHttpAsync(ct);
        log.LogInformation("снимок каталога: {Count} строк, источник=http", httpRows.Count);
        return httpRows;
    }

    public async Task<bool> ProductExistsAsync(Guid id, CancellationToken ct = default)
    {
        var path = $"api/catalog/{id}";
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log.LogWarning("Catalog {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
            response.EnsureSuccessStatusCode();
        }
        return true;
    }

    async Task<int> CountFromSqlAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(CountSql, conn);
        var n = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(n);
    }

    async Task<IReadOnlyList<CatalogProductSnapshot>> ListFromSqlAsync(CancellationToken ct)
    {
        var acc = new List<CatalogProductSnapshot>(8192);
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(SnapshotSql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            acc.Add(new CatalogProductSnapshot(
                r.GetGuid(0),
                r.GetString(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4)));
        }
        return acc;
    }

    async Task<IReadOnlyList<CatalogProductSnapshot>> ListFromHttpAsync(CancellationToken ct)
    {
        var first = await GetBrowsePageAsync(1, BrowsePageSize, ct);
        if (first is null) return [];
        var total = first.Total;
        var pages = Math.Min(MaxBrowsePages, Math.Max(1, (int)Math.Ceiling(total / (double)BrowsePageSize)));
        var acc = new CatalogProductSnapshot?[Math.Max(total, 0)];
        void Copy(IReadOnlyList<CardJson> items, int page)
        {
            var offset = (page - 1) * BrowsePageSize;
            foreach (var card in items)
            {
                var i = offset++;
                if ((uint)i < (uint)acc.Length)
                    acc[i] = ToSnapshot(card);
            }
        }
        Copy(first.Items ?? [], 1);
        if (pages == 1) return acc.OfType<CatalogProductSnapshot>().ToList();

        using var gate = new SemaphoreSlim(BrowseConcurrency);
        var tasks = Enumerable.Range(2, pages - 1).Select(async page =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var batch = await GetBrowsePageAsync(page, BrowsePageSize, ct);
                if (batch?.Items is { Count: > 0 } items)
                    Copy(items, page);
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
        return acc.OfType<CatalogProductSnapshot>().ToList();
    }

    async Task<PagedJson<CardJson>?> GetBrowsePageAsync(int page, int pageSize, CancellationToken ct) =>
        await GetOrNullAsync<PagedJson<CardJson>>($"api/catalog?page={page}&pageSize={pageSize}", ct);

    async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log.LogWarning("Catalog {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
            response.EnsureSuccessStatusCode();
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    static CatalogProductSnapshot ToSnapshot(CardJson c) =>
        new(c.Id, c.Sku, c.Name, c.Manufacturer, c.Category);

    static string Trim(string s) => s.Length <= 400 ? s : s[..400];

    sealed record PagedJson<T>(IReadOnlyList<T>? Items, int Total, int Page, int PageSize);
    sealed record CardJson(Guid Id, string Sku, string Name, string? Manufacturer, string? Category);
}
