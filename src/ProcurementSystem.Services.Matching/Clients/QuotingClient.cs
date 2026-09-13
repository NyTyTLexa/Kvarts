using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcurementSystem.Services.Matching.Clients;

public interface IQuotingClient
{
    Task<QuotingSpecDetail?> GetSpecificationAsync(Guid id, CancellationToken ct = default);
    Task<bool> SetItemProductAsync(Guid specId, Guid itemId, Guid productId, CancellationToken ct = default);
}

public record QuotingSpecDetail(Guid Id, string Title, string? Customer, IReadOnlyList<QuotingSpecItem> Items);
public record QuotingSpecItem(Guid Id, Guid? ProductId, string? Sku, string Name, int Quantity, bool Matched);

/// <summary>
/// Спецификации живут у процесса Quoting. Вызов прямой (localhost:5170 / quoting:8080), не через шлюз.
/// </summary>
public sealed class QuotingClient(HttpClient http, ILogger<QuotingClient> log) : IQuotingClient
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<QuotingSpecDetail?> GetSpecificationAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<QuotingSpecDetail>($"api/specifications/{id}", ct);

    public async Task<bool> SetItemProductAsync(Guid specId, Guid itemId, Guid productId, CancellationToken ct = default)
    {
        var path = $"api/specifications/{specId}/match/apply-one";
        using var response = await http.PostAsJsonAsync(path, new { itemId, productId }, Json, ct);
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK) return true;
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        var body = await response.Content.ReadAsStringAsync(ct);
        log.LogWarning("Quoting {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
        response.EnsureSuccessStatusCode();
        return false;
    }

    async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log.LogWarning("Quoting {Path} → {Status}: {Body}", path, (int)response.StatusCode, Trim(body));
            response.EnsureSuccessStatusCode();
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    static string Trim(string s) => s.Length <= 400 ? s : s[..400];
}
