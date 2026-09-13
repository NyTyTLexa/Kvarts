using System.Diagnostics;

namespace ProcurementSystem.Services.Ordering.Observability;

/// <summary>
/// Источник собственных трасс сервиса заказов — для бизнес-операций,
/// которые не покрываются авто-инструментацией (HTTP/EF).
/// </summary>
public static class Telemetry
{
    public const string SourceName = "procurement-ordering";
    public static readonly ActivitySource Source = new(SourceName);
}
