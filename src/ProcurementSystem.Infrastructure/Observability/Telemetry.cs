using System.Diagnostics;

namespace ProcurementSystem.Infrastructure.Observability;

/// <summary>
/// Источник собственных трасс приложения — для бизнес-операций (генерация КП, индексация),
/// которые не покрываются авто-инструментацией (HTTP/EF/NATS).
/// </summary>
public static class Telemetry
{
    public const string SourceName = "ProcurementSystem";
    public static readonly ActivitySource Source = new(SourceName);
}
