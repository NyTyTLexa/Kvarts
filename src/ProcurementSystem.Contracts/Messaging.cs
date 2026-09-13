namespace ProcurementSystem.Contracts;

/// <summary>Общие константы обмена сообщениями (Api, Worker, Logistics, Commercial).</summary>
public static class Messaging
{
    public const string Stream = "procurement";
    public const string SubjectWildcard = "procurement.>";

    /// <summary>Subject события по имени типа: procurement.{TypeName}.</summary>
    public static string Subject(string type) => $"procurement.{type}";
}
