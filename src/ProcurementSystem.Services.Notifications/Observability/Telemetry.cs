using System.Diagnostics;

namespace ProcurementSystem.Services.Notifications.Observability;

/// <summary>
/// Источник собственных трасс приложения — для бизнес-операций (рассылка email),
/// которые не покрываются авто-инструментацией (HTTP/EF).
/// </summary>
public static class Telemetry
{
    public const string SourceName = "ProcurementSystem";
    public static readonly ActivitySource Source = new(SourceName);
}
