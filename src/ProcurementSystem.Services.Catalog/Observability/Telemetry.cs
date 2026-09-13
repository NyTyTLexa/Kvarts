using System.Diagnostics;

namespace ProcurementSystem.Services.Catalog.Observability;

/// <summary>
/// Источник собственных трасс — для операций, которые не покрывает авто-инструментация (HTTP/EF/NATS).
/// </summary>
public static class Telemetry
{
    public const string SourceName = "ProcurementSystem";
    public static readonly ActivitySource Source = new(SourceName);
}
