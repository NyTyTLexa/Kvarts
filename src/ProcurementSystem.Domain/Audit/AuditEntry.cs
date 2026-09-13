using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Audit;

/// <summary>Запись журнала действий: кто, что и когда изменил через API.</summary>
public class AuditEntry : Entity
{
    public string? UserName { get; set; }                  // preferred_username из токена
    public string Action { get; set; } = default!;         // HTTP-метод (POST/PUT/DELETE)
    public string Path { get; set; } = default!;           // путь запроса
    public int StatusCode { get; set; }                    // результат
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? ChangesJson { get; set; }               // изменения сущностей в JSON
}

public sealed record AuditChange(
    string Entity,
    string EntityId,
    string Property,
    string? OldValue,
    string? NewValue);
