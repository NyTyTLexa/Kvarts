using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProcurementSystem.Services.Logistics.Ordering;

/// <summary>Снимок заказа, достаточный чтобы собрать приёмку и заполнить событие. Чужие поля не тащим.</summary>
public sealed record OrderSnapshot(
    Guid Id,
    string Number,
    string? Customer,
    IReadOnlyList<OrderSnapshotLine> Lines,
    Guid? SpecificationId = null);

public sealed record OrderSnapshotLine(
    Guid? ProductId,
    string? Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);

public interface IOrderReader
{
    Task<OrderSnapshot?> GetAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// Заказы принадлежат соседу. После 12.5 <c>Services:Ordering</c> указывает
/// на вынесенный Ordering (:5171 / Docker DNS ordering), маршрут тот же: GET /api/orders/{id}.
/// </summary>
public sealed class OrderHttpClient(
    HttpClient http,
    IHttpContextAccessor httpContext,
    ILogger<OrderHttpClient> log) : IOrderReader
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<OrderSnapshot?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"api/orders/{orderId}");
        var auth = httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth))
            req.Headers.TryAddWithoutValidation("Authorization", auth);

        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning("Соседний сервис заказов ответил {Status} на заказ {OrderId}", (int)res.StatusCode, orderId);
            res.EnsureSuccessStatusCode();
        }

        var dto = await res.Content.ReadFromJsonAsync<OrderSnapshotDto>(Json, ct);
        if (dto is null || dto.Id == Guid.Empty)
            return null;

        return new OrderSnapshot(dto.Id, dto.Number, dto.Customer, dto.Lines ?? [], dto.SpecificationId);
    }

    sealed record OrderSnapshotDto(
        Guid Id,
        string Number,
        string? Customer,
        IReadOnlyList<OrderSnapshotLine>? Lines,
        Guid? SpecificationId);
}
