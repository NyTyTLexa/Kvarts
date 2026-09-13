using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ProcurementSystem.Services.Ordering;

namespace ProcurementSystem.Services.Ordering.Clients;

public sealed record QuoteSnapshot(
    string SpecificationTitle,
    decimal TotalCost,
    int MaxLeadTimeDays,
    IReadOnlyList<QuoteSnapshotLine> Lines);

public sealed record QuoteSnapshotLine(
    bool Matched,
    Guid? ProductId,
    string? Sku,
    string Name,
    int Quantity,
    Guid? VendorId,
    string? VendorName,
    decimal UnitPrice,
    decimal LineTotal,
    int LeadTimeDays);

public interface IQuoteClient
{
    Task<QuoteSnapshot?> GetQuoteAsync(Guid specId, QuoteStrategy strategy, double wPrice, double wLead, bool onlyInStock, CancellationToken ct);
    Task<string?> GetCustomerAsync(Guid specId, CancellationToken ct);
}

public class QuoteClient(HttpClient http, IConfiguration config) : IQuoteClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _baseUrl = TrimBase(config["Neighbors:Quoting:BaseUrl"]);

    public async Task<QuoteSnapshot?> GetQuoteAsync(Guid specId, QuoteStrategy strategy, double wPrice, double wLead, bool onlyInStock, CancellationToken ct)
    {
        var url =
            $"{_baseUrl}/api/specifications/{specId}/quote" +
            $"?strategy={Uri.EscapeDataString(strategy.ToString())}" +
            $"&wPrice={wPrice.ToString(CultureInfo.InvariantCulture)}" +
            $"&wLead={wLead.ToString(CultureInfo.InvariantCulture)}" +
            $"&onlyInStock={(onlyInStock ? "true" : "false")}";

        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuoteSnapshot>(JsonOpts, ct);
    }

    public async Task<string?> GetCustomerAsync(Guid specId, CancellationToken ct)
    {
        var url = $"{_baseUrl}/api/specifications/{specId}";
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<SpecificationCustomerDto>(JsonOpts, ct);
        return dto?.Customer;
    }

    private static string TrimBase(string? value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "http://localhost:5165" : value.Trim();
        return raw.TrimEnd('/');
    }

    private sealed record SpecificationCustomerDto(string? Customer);
}
