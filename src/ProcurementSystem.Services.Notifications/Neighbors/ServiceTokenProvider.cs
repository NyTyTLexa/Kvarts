using System.Text.Json;

namespace ProcurementSystem.Services.Notifications.Neighbors;

/// <summary>
/// Service token для опроса соседей без пользовательского контекста.
/// Password grant, client_id=procurement-api; кэш до expires_in − 30 с.
/// </summary>
public sealed class ServiceTokenProvider(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ServiceTokenProvider> log)
{
    public const string HttpClientName = "keycloak-token";

    readonly SemaphoreSlim _gate = new(1, 1);
    string? _token;
    DateTimeOffset _expiresAt;

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (HasFreshToken()) return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (HasFreshToken()) return _token;

            var http = httpFactory.CreateClient(HttpClientName);
            var tokenUrl = config["Neighbors:TokenUrl"];
            if (string.IsNullOrWhiteSpace(tokenUrl))
            {
                var authority = (config["Keycloak:Authority"] ?? "http://localhost:8088/realms/procurement").TrimEnd('/');
                tokenUrl = $"{authority}/protocol/openid-connect/token";
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "procurement-api",
                ["username"] = config["Neighbors:ServiceUser"] ?? "admin",
                ["password"] = config["Neighbors:ServicePassword"] ?? "admin",
            });

            using var response = await http.PostAsync(tokenUrl, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Не удалось получить service token: {Status} {Url}",
                    (int)response.StatusCode, tokenUrl);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                log.LogWarning("Keycloak вернул пустой access_token");
                return null;
            }

            var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds)
                ? seconds
                : 300;
            _token = accessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, expiresIn - 30));
            return _token;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Ошибка получения service token");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    bool HasFreshToken() => _token is not null && DateTimeOffset.UtcNow < _expiresAt;
}
