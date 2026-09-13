using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Audit;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Audit;

public record AuditEntryDto(
    string? UserName,
    string Action,
    string Path,
    int StatusCode,
    DateTime OccurredAtUtc,
    IReadOnlyList<AuditChange>? Changes);

/// <summary>
/// Настройки ретенции журнала аудита (ТЗ: «рекомендуется 3 года»).
/// RetentionDays — срок хранения записей; старше этого порога удаляются фоновым сервисом
/// AuditRetentionService, чтобы таблица не росла бесконечно.
/// CleanupIntervalHours — как часто запускать очистку.
/// </summary>
public class AuditOptions
{
    public int RetentionDays { get; set; } = 1095;          // ~3 года (3×365)
    public int CleanupIntervalHours { get; set; } = 6;
}

public interface IAuditService
{
    Task RecordAsync(string? userName, string action, string path, int statusCode, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntryDto>> RecentAsync(int limit, CancellationToken ct = default);
}

/// <summary>Журнал действий пользователей (мутации через API).</summary>
public class AuditService(AppDbContext db, AuditRequestContext request) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task RecordAsync(string? userName, string action, string path, int statusCode, CancellationToken ct = default)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            UserName = userName,
            Action = action,
            Path = path,
            StatusCode = statusCode,
            OccurredAtUtc = request.OccurredAtUtc
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntryDto>> RecentAsync(int limit, CancellationToken ct = default)
    {
        var entries = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct);

        return entries.Select(a => new AuditEntryDto(
                a.UserName,
                a.Action,
                a.Path,
                a.StatusCode,
                a.OccurredAtUtc,
                ParseChanges(a.ChangesJson)))
            .ToList();
    }

    private static IReadOnlyList<AuditChange>? ParseChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<AuditChange>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
