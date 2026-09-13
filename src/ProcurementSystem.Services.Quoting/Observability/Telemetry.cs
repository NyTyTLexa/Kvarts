using System.Diagnostics;

namespace ProcurementSystem.Services.Quoting.Observability;

/// <summary>
/// Источник собственных трасс — для операций, которые не покрывает авто-инструментация (HTTP/EF).
/// </summary>
public static class Telemetry
{
    public const string SourceName = "ProcurementSystem";
    public static readonly ActivitySource Source = new(SourceName);
}
