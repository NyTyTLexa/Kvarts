using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProcurementSystem.Services.Ordering.Clients;

public interface ICatalogStockClient
{
    Task AdjustAsync(IEnumerable<(Guid? ProductId, Guid? VendorId, int Quantity)> lines, int sign, CancellationToken ct);
}

/// <summary>
/// Резерв/возврат остатка через каталог. Эндпоинта у монолита нет;
/// 404/405 логируются и не роняют оформление заказа.
/// </summary>
public class CatalogStockClient(HttpClient http, IConfiguration config, ILogger<CatalogStockClient> logger) : ICatalogStockClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _baseUrl = TrimBase(config["Neighbors:Catalog:BaseUrl"]);

    public async Task AdjustAsync(IEnumerable<(Guid? ProductId, Guid? VendorId, int Quantity)> lines, int sign, CancellationToken ct)
    {
        var items = lines
            .Where(l => l.ProductId is not null && l.VendorId is not null)
            .Select(l => new StockAdjustItem(l.ProductId!.Value, l.VendorId!.Value, l.Quantity, sign))
            .ToList();
        if (items.Count == 0) return;

        var url = $"{_baseUrl}/api/catalog/stock/adjust";
        using var response = await http.PostAsJsonAsync(url, new StockAdjustRequest(items), JsonOpts, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            logger.LogWarning(
                "Catalog stock adjust skipped: {Status} {Reason} {Url}",
                (int)response.StatusCode, response.ReasonPhrase, url);
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private static string TrimBase(string? value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "http://localhost:5165" : value.Trim();
        return raw.TrimEnd('/');
    }

    private sealed record StockAdjustRequest(IReadOnlyList<StockAdjustItem> Items);
    private sealed record StockAdjustItem(Guid ProductId, Guid VendorId, int Quantity, int Sign);
}
