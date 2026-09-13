using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ProcurementSystem.Auth.Keycloak;

public sealed record KeycloakUser(
    string Id, string UserName, string? Email, string DisplayName,
    IReadOnlyList<string> Roles, bool Enabled);

public sealed record TokenBundle(
    string AccessToken, string? RefreshToken, string? IdToken,
    int ExpiresIn, string TokenType, string Scope);

public interface IKeycloakClient
{
    Task<(bool Ok, string? Error, bool Conflict)> CreateUserAsync(
        string username, string email, string firstName, string lastName,
        string password, string? company, CancellationToken ct = default);

    Task<(TokenBundle? Tokens, string? Error)> PasswordLoginAsync(
        string username, string password, CancellationToken ct = default);

    Task<IReadOnlyList<KeycloakUser>> ListUsersAsync(CancellationToken ct = default);
}

public sealed class KeycloakClient(IHttpClientFactory httpFactory, IOptions<KeycloakOptions> options)
    : IKeycloakClient
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    readonly SemaphoreSlim _gate = new(1, 1);
    string? _adminToken;
    DateTime _adminUntil = DateTime.MinValue;

    public async Task<(bool Ok, string? Error, bool Conflict)> CreateUserAsync(
        string username, string email, string firstName, string lastName,
        string password, string? company, CancellationToken ct = default)
    {
        HttpClient http;
        string? token;
        try
        {
            http = Client();
            token = await AdminTokenAsync(ct);
        }
        catch (Exception)
        {
            return (false, "Нет связи с Keycloak на " + Opt.AdminBaseUrl + ".", false);
        }
        if (token is null) return (false, "Keycloak недоступен — не удалось получить admin-токен.", false);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"admin/realms/{Opt.Realm}/users");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = new Dictionary<string, object?>
        {
            ["username"] = username,
            ["email"] = email,
            ["firstName"] = firstName,
            ["lastName"] = lastName,
            ["enabled"] = true,
            ["emailVerified"] = true,
            ["credentials"] = new object[] { new { type = "password", value = password, temporary = false } },
        };
        if (!string.IsNullOrWhiteSpace(company))
            body["attributes"] = new Dictionary<string, string[]> { ["company"] = [company.Trim()] };
        req.Content = JsonContent(body);

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ct); }
        catch (Exception)
        {
            return (false, "Нет связи с Keycloak на " + Opt.AdminBaseUrl + ".", false);
        }
        using (resp)
        {
        if (resp.StatusCode == HttpStatusCode.Conflict)
            return (false, "Такой логин или email уже есть.", true);
        if (resp.StatusCode is not HttpStatusCode.Created and not HttpStatusCode.NoContent)
            return (false, await ReadError(resp, "Не удалось создать пользователя в Keycloak."), false);

        var id = UserIdFromLocation(resp) ?? await FindUserIdAsync(http, token, username, ct);
        if (id is not null) await AssignRoleAsync(http, token, id, Opt.DefaultRole, ct);
        return (true, null, false);
        }
    }

    public async Task<(TokenBundle? Tokens, string? Error)> PasswordLoginAsync(
        string username, string password, CancellationToken ct = default)
    {
        var http = Client();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"realms/{Opt.Realm}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = Opt.PublicClientId,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid profile email",
            }),
        };
        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ct); }
        catch (Exception)
        {
            return (null, "Нет связи с Keycloak на " + Opt.AdminBaseUrl + ".");
        }
        using (resp)
        {
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return (null, resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.BadRequest
                ? "Неверный логин или пароль."
                : "Keycloak отклонил вход.");
        var tok = JsonSerializer.Deserialize<KcToken>(raw, Json);
        if (string.IsNullOrWhiteSpace(tok?.AccessToken)) return (null, "Keycloak не вернул access_token.");
        return (new TokenBundle(
            tok.AccessToken,
            tok.RefreshToken,
            tok.IdToken,
            tok.ExpiresIn ?? 3600,
            tok.TokenType ?? "Bearer",
            tok.Scope ?? "openid profile email"), null);
        }
    }

    public async Task<IReadOnlyList<KeycloakUser>> ListUsersAsync(CancellationToken ct = default)
    {
        try
        {
            var http = Client();
            var token = await AdminTokenAsync(ct);
            if (token is null)
                throw new InvalidOperationException("Keycloak не выдал admin-токен (проверьте 8088 и учётку admin/admin).");
            using var req = new HttpRequestMessage(HttpMethod.Get, $"admin/realms/{Opt.Realm}/users?max=200");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Keycloak users → HTTP {(int)resp.StatusCode}.");
            var rows = JsonSerializer.Deserialize<List<KcUser>>(await resp.Content.ReadAsStringAsync(ct), Json) ?? [];
            var list = new List<KeycloakUser>();
            foreach (var u in rows)
            {
                if (string.IsNullOrWhiteSpace(u.Username) || string.IsNullOrWhiteSpace(u.Id)) continue;
                var roles = await RealmRolesAsync(http, token, u.Id, ct);
                var display = string.Join(' ', new[] { u.FirstName, u.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                list.Add(new KeycloakUser(u.Id, u.Username, u.Email, string.IsNullOrWhiteSpace(display) ? u.Username : display, roles, u.Enabled));
            }
            return list;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Нет связи с Keycloak на " + Opt.AdminBaseUrl + ".", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException("Keycloak не ответил вовремя.", ex);
        }
    }

    async Task AssignRoleAsync(HttpClient http, string token, string userId, string roleName, CancellationToken ct)
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, $"admin/realms/{Opt.Realm}/roles/{Uri.EscapeDataString(roleName)}");
        get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var roleResp = await http.SendAsync(get, ct);
        if (!roleResp.IsSuccessStatusCode) return;
        var roleJson = await roleResp.Content.ReadAsStringAsync(ct);
        using var map = new HttpRequestMessage(HttpMethod.Post, $"admin/realms/{Opt.Realm}/users/{userId}/role-mappings/realm");
        map.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        map.Content = new StringContent("[" + roleJson + "]", System.Text.Encoding.UTF8, "application/json");
        _ = await http.SendAsync(map, ct);
    }

    async Task<IReadOnlyList<string>> RealmRolesAsync(HttpClient http, string token, string userId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"admin/realms/{Opt.Realm}/users/{userId}/role-mappings/realm");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return [];
        var roles = JsonSerializer.Deserialize<List<KcRole>>(await resp.Content.ReadAsStringAsync(ct), Json) ?? [];
        return roles.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().ToList();
    }

    async Task<string?> FindUserIdAsync(HttpClient http, string token, string username, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"admin/realms/{Opt.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var rows = JsonSerializer.Deserialize<List<KcUser>>(await resp.Content.ReadAsStringAsync(ct), Json);
        return rows?.FirstOrDefault()?.Id;
    }

    async Task<string?> AdminTokenAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_adminToken is not null && DateTime.UtcNow < _adminUntil) return _adminToken;
            var http = Client();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"realms/{Opt.AdminRealm}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = Opt.AdminClientId,
                    ["username"] = Opt.AdminUsername,
                    ["password"] = Opt.AdminPassword,
                }),
            };
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var tok = JsonSerializer.Deserialize<KcToken>(await resp.Content.ReadAsStringAsync(ct), Json);
            if (string.IsNullOrWhiteSpace(tok?.AccessToken)) return null;
            _adminToken = tok.AccessToken;
            var seconds = tok.ExpiresIn is > 30 ? tok.ExpiresIn.Value - 20 : 40;
            _adminUntil = DateTime.UtcNow.AddSeconds(seconds);
            return _adminToken;
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
        finally { _gate.Release(); }
    }

    HttpClient Client() => httpFactory.CreateClient("keycloak");
    KeycloakOptions Opt => options.Value;

    static string? UserIdFromLocation(HttpResponseMessage resp)
    {
        var loc = resp.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(loc)) return null;
        var i = loc.LastIndexOf('/');
        return i < 0 || i == loc.Length - 1 ? null : loc[(i + 1)..];
    }

    static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    static async Task<string> ReadError(HttpResponseMessage resp, string fallback)
    {
        var t = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(t)) return $"{fallback} (HTTP {(int)resp.StatusCode})";
        try
        {
            using var doc = JsonDocument.Parse(t);
            if (doc.RootElement.TryGetProperty("errorMessage", out var m) && m.GetString() is { Length: > 0 } msg)
                return msg;
        }
        catch (JsonException) { }
        return t.Length > 180 ? t[..180] + "…" : t;
    }

    sealed class KcToken
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
        [JsonPropertyName("expires_in")] public int? ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }

    sealed class KcUser
    {
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Enabled { get; set; }
    }

    sealed class KcRole { public string? Name { get; set; } }
}
