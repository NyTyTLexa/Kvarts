using ProcurementSystem.Infrastructure.Audit;

namespace ProcurementSystem.Api.Auditing;

/// <summary>
/// Пишет запись в журнал аудита после каждого изменяющего (POST/PUT/DELETE/PATCH) запроса
/// к /api от аутентифицированного пользователя. Сбои аудита не влияют на основной запрос.
/// </summary>
public class AuditMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> Mutating =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    public async Task InvokeAsync(HttpContext ctx, AuditRequestContext requestAudit)
    {
        requestAudit.UserName = ctx.User.Identity?.Name;
        requestAudit.Action = ctx.Request.Method;
        requestAudit.Path = ctx.Request.Path + ctx.Request.QueryString;
        requestAudit.OccurredAtUtc = DateTime.UtcNow;

        await next(ctx);

        try
        {
            if (Mutating.Contains(ctx.Request.Method)
                && ctx.Request.Path.StartsWithSegments("/api")
                && ctx.User.Identity?.IsAuthenticated == true)
            {
                var audit = ctx.RequestServices.GetRequiredService<IAuditService>();
                await audit.RecordAsync(
                    ctx.User.Identity?.Name,
                    ctx.Request.Method,
                    ctx.Request.Path + ctx.Request.QueryString,
                    ctx.Response.StatusCode);
            }
        }
        catch
        {
            // журнал аудита не должен ломать обработку запроса
        }
    }
}
