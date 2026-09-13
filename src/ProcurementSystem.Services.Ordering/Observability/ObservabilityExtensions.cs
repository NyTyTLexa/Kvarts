using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace ProcurementSystem.Services.Ordering.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Наблюдаемость (Этап 6): структурные логи (Serilog) + распределённая трассировка и метрики
    /// (OpenTelemetry → OTLP → Jaeger). Подключается одинаково в Api и Worker — отсюда общий код.
    /// Эндпоинт OTLP берётся из конфигурации (Otlp:Endpoint), по умолчанию gRPC :4317.
    /// </summary>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        var otlp = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");

        // --- Структурные логи (Serilog заменяет стандартный логгер) ---
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, cfg) => cfg
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.With<ActivityTraceEnricher>()
            .Enrich.WithProperty("service", serviceName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.OpenTelemetry(o =>
            {
                o.Endpoint = builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317";
                o.Protocol = OtlpProtocol.Grpc;
                o.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName
                };
            }));

        // --- Трассировка и метрики (OpenTelemetry → OTLP → Jaeger) ---
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddSource(Telemetry.SourceName)
                .AddSource("NATS.Net")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = otlp))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = otlp));

        return builder;
    }
}

file sealed class ActivityTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null || activity.IdFormat != ActivityIdFormat.W3C || activity.TraceId == default)
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("trace_id", activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("span_id", activity.SpanId.ToHexString()));
    }
}
