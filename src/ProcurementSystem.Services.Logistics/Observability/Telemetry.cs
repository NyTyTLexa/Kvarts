using System.Diagnostics;

namespace ProcurementSystem.Services.Logistics.Observability;

/// <summary>
/// Источник собственных трасс приложения — для бизнес-операций (приёмка, выгрузка в WMS/1С),
/// которые не покрываются авто-инструментацией (HTTP/EF/NATS).
/// </summary>
public static class Telemetry
{
    public const string SourceName = "ProcurementSystem";
    public static readonly ActivitySource Source = new(SourceName);
}
