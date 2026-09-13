using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProcurementSystem.Domain.Audit;

namespace ProcurementSystem.Infrastructure.Audit;

public sealed class AuditRequestContext
{
    public string? UserName { get; set; }
    public string Action { get; set; } = "SAVE";
    public string Path { get; set; } = "EF SaveChanges";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Фиксирует значения полей выбранных сущностей до сохранения.</summary>
public sealed class AuditSaveChangesInterceptor(AuditRequestContext request) : SaveChangesInterceptor
{
    private static readonly HashSet<string> AuditedEntities =
        new(StringComparer.Ordinal)
        {
            "Discount",
            "Order",
            "Invoice",
            "Approval",
            "GoodsReceipt",
            "PriceListItem"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AppendAuditEntry(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntry(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void AppendAuditEntry(DbContext? db)
    {
        if (db is null)
            return;

        db.ChangeTracker.DetectChanges();
        var changes = db.ChangeTracker.Entries()
            .Where(e => AuditedEntities.Contains(e.Metadata.ClrType.Name)
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        // Новые офферы массового импорта прайса — шум (их цену фиксирует история цен);
                        // в журнал идут только изменения существующих офферов.
                        && !(e.State == EntityState.Added && e.Metadata.ClrType.Name == "PriceListItem"))
            .SelectMany(e => ChangedProperties(e).Select(p => new AuditChange(
                e.Metadata.ClrType.Name,
                EntityId(e),
                p.Metadata.Name,
                e.State == EntityState.Added ? null : FormatValue(p.OriginalValue),
                e.State == EntityState.Deleted ? null : FormatValue(p.CurrentValue))))
            .ToList();

        if (changes.Count == 0)
            return;

        db.Set<AuditEntry>().Add(new AuditEntry
        {
            UserName = request.UserName,
            Action = request.Action,
            Path = request.Path,
            StatusCode = 200,
            OccurredAtUtc = request.OccurredAtUtc,
            ChangesJson = JsonSerializer.Serialize(changes, JsonOptions)
        });
    }

    private static IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry> ChangedProperties(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.ClrType == typeof(byte[]))
                continue;

            if (entry.State == EntityState.Modified)
            {
                if (!property.IsModified || ValuesEqual(property.OriginalValue, property.CurrentValue))
                    continue;
            }
            else if (entry.State == EntityState.Added && property.CurrentValue is null)
            {
                continue;
            }
            else if (entry.State == EntityState.Deleted && property.OriginalValue is null)
            {
                continue;
            }

            yield return property;
        }
    }

    private static bool ValuesEqual(object? oldValue, object? newValue) =>
        oldValue switch
        {
            byte[] oldBytes when newValue is byte[] newBytes => oldBytes.SequenceEqual(newBytes),
            _ => Equals(oldValue, newValue)
        };

    private static string EntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
            return string.Empty;

        return string.Join(",", key.Properties.Select(p =>
            FormatValue(entry.State == EntityState.Deleted
                ? entry.Property(p.Name).OriginalValue
                : entry.Property(p.Name).CurrentValue) ?? string.Empty));
    }

    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        Enum enumValue => enumValue.ToString(),
        bool boolValue => boolValue ? "true" : "false",
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
