using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

// Шлюз (API Gateway) — единственная точка входа для SPA. Маршрутизирует /api/* по
// сервисам. Пока сервисы вынимаются из монолита по одному, «нераспиленные» маршруты
// уходят в него же (catch-all в конце таблицы) — система остаётся рабочей на каждом шаге.

var builder = WebApplication.CreateBuilder(args);

var otlp = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");

builder.Logging.ClearProviders();
builder.Services.AddSerilog((_, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<ActivityTraceEnricher>()
    .Enrich.WithProperty("service", "procurement-gateway")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.OpenTelemetry(o =>
    {
        o.Endpoint = builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317";
        o.Protocol = OtlpProtocol.Grpc;
        o.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "procurement-gateway"
        };
    }));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("procurement-gateway"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        // Исходящие запросы к сервисам: без этого трасса обрывается на шлюзе
        // и в Grafana не видно, к кому он ходил.
        .AddHttpClientInstrumentation(o => o.RecordException = true)
        .AddOtlpExporter(o => o.Endpoint = otlp));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseSerilogRequestLogging();

// Здоровье самого шлюза. Здоровье сервисов за ним — их собственные /health,
// их опрашивает docker compose, а не шлюз.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" }));

app.MapReverseProxy();

app.Run();

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
