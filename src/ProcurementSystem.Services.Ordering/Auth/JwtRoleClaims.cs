using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ProcurementSystem.Services.Ordering.Auth;

/// <summary>
/// Keycloak кладёт роли в JSON-объект realm_access; JsonWebTokenHandler часто
/// не делает из него ClaimTypes.Role — без этого POST /api/pricelist/import даёт 403.
/// </summary>
public static class JwtRoleClaims
{
    static readonly HashSet<string> AppRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "manager", "viewer", "commercial", "accounting", "warehouse",
    };

    public static void CopyToIdentity(TokenValidatedContext ctx)
    {
        try { CopyToIdentityCore(ctx); }
        catch { /* разбор ролей не должен отдавать 500 */ }
    }

    static void CopyToIdentityCore(TokenValidatedContext ctx)
    {
        if (ctx.Principal?.Identity is not ClaimsIdentity identity) return;
        var seen = new HashSet<string>(identity.FindAll(identity.RoleClaimType).Select(c => c.Value), StringComparer.OrdinalIgnoreCase);

        void Add(string? role)
        {
            if (string.IsNullOrWhiteSpace(role) || !AppRoles.Contains(role) || !seen.Add(role)) return;
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var c in identity.Claims)
        {
            if (c.Type is "role" or "roles" || c.Type == ClaimTypes.Role || c.Type == identity.RoleClaimType)
                Add(c.Value);
            else if (c.Type.Contains("realm_access", StringComparison.OrdinalIgnoreCase)
                     || c.Type.Contains("resource_access", StringComparison.OrdinalIgnoreCase))
                FromJson(c.Value, Add);
        }

        if (ctx.SecurityToken is JsonWebToken jwt)
        {
            if (jwt.TryGetPayloadValue("realm_access", out JsonElement realm)) FromElement(realm, Add);
            if (jwt.TryGetPayloadValue("roles", out JsonElement roles)) FromElement(roles, Add);
            if (jwt.TryGetPayloadValue("resource_access", out JsonElement resources)
                && resources.ValueKind == JsonValueKind.Object)
            {
                foreach (var client in resources.EnumerateObject())
                    if (client.Value.TryGetProperty("roles", out var rs))
                        FromElement(rs, Add);
            }
        }
    }

    static void FromJson(string value, Action<string?> add)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value[0] is not '{' and not '[') { add(value); return; }
        try
        {
            using var doc = JsonDocument.Parse(value);
            FromElement(doc.RootElement, add);
        }
        catch (JsonException) { /* не JSON — обычный claim */ }
    }

    static void FromElement(JsonElement el, Action<string?> add)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var x in el.EnumerateArray()) add(x.GetString());
                break;
            case JsonValueKind.Object when el.TryGetProperty("roles", out var roles):
                FromElement(roles, add);
                break;
        }
    }
}
