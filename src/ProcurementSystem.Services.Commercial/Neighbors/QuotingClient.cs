using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProcurementSystem.Services.Commercial.Commercial;

namespace ProcurementSystem.Services.Commercial.Neighbors;

/// <summary>
/// Спецификация и КП живут у соседа (сервис Quoting).
/// Коммерческий сервис не держит их таблицы и не копирует генератор.
/// </summary>
public interface IQuotingClient
{
    Task<NeighborSpecification?> GetSpecificationAsync(Guid id, CancellationToken ct = default);
    Task<NeighborQuote?> GenerateQuoteAsync(Guid specificationId, QuoteStrategy strategy, CancellationToken ct = default);
}

public record NeighborSpecification(Guid Id, string Title, string? Customer);

public record NeighborQuote(
    Guid SpecificationId,
    string SpecificationTitle,
    decimal TotalCost,
    IReadOnlyList<NeighborQuoteLine> Lines);

public record NeighborQuoteLine(
    Guid? ProductId,
    string? Sku,
    string Name,
    int Quantity,
    bool Matched,
    string? VendorName,
    string? Manufacturer,
    decimal UnitPriceDiscounted);

/// <summary>Прокидывает Bearer входящего запроса к соседу — quoting тоже требует JWT.</summary>
public sealed class ForwardAuthorizationHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming) && AuthenticationHeaderValue.TryParse(incoming, out var header))
            request.Headers.Authorization = header;
        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class QuotingClient(HttpClient http) : IQuotingClient
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<NeighborSpecification?> GetSpecificationAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<NeighborSpecification>($"api/specifications/{id}", ct);

    public Task<NeighborQuote?> GenerateQuoteAsync(Guid specificationId, QuoteStrategy strategy, CancellationToken ct = default) =>
        GetOrNullAsync<NeighborQuote>($"api/specifications/{specificationId}/quote?strategy={strategy}", ct);

    async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Сервис quoting ответил {(int)response.StatusCode} {response.ReasonPhrase} на {path}: {Trim(body)}",
                null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    static string Trim(string s) => s.Length <= 400 ? s : s[..400];
}
