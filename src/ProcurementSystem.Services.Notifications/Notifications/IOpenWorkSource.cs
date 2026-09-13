namespace ProcurementSystem.Services.Notifications.Notifications;

public record OpenWorkItem(string Type, string Role, string Title, string Message, string EntityType, Guid EntityId, DateTime CreatedAtUtc);

public interface IOpenWorkSource
{
    Task<IReadOnlyList<OpenWorkItem>> GetOpenWorkAsync(CancellationToken ct = default);
}
