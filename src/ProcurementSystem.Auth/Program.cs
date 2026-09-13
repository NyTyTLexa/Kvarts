using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProcurementSystem.Auth.Keycloak;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

var otlp = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");

builder.Logging.ClearProviders();
builder.Services.AddSerilog((_, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<ActivityTraceEnricher>()
    .Enrich.WithProperty("service", "procurement-auth")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.OpenTelemetry(o =>
    {
        o.Endpoint = builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317";
        o.Protocol = OtlpProtocol.Grpc;
        o.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "procurement-auth"
        };
    }));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("procurement-auth"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation(o => o.RecordException = true)
        .AddOtlpExporter(o => o.Endpoint = otlp));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT из этого же сервиса (POST /api/auth/login).",
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() },
    });
});

builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection(KeycloakOptions.Section));
builder.Services.AddHttpClient("keycloak", (sp, http) =>
{
    var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakOptions>>().Value;
    http.BaseAddress = new Uri(o.AdminBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<IKeycloakClient, KeycloakClient>();

var kc = builder.Configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => ConfigureKeycloakJwt(o, kc));

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("admin", p => p.RequireRole("admin"));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost", "http://127.0.0.1:5173")
        .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseCors();
if (app.Environment.IsDevelopment())
    app.UseSwagger(o => o.RouteTemplate = "/openapi/{documentName}.json");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "auth" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(o => o.WithTitle("Auth service")).AllowAnonymous();
    app.MapGet("/swagger", () => Results.Redirect("/scalar")).AllowAnonymous();
}

app.Run();

static void ConfigureKeycloakJwt(JwtBearerOptions o, IConfigurationSection kc)
{
    var authority = kc["Authority"] ?? "http://localhost:8088/realms/procurement";
    o.Authority = authority;
    o.Audience = kc["Audience"];
    o.RequireHttpsMetadata = kc.GetValue("RequireHttpsMetadata", false);
    o.IncludeErrorDetails = true;
    o.MapInboundClaims = false;
    var metadata = kc["MetadataAddress"];
    if (!string.IsNullOrWhiteSpace(metadata))
        o.MetadataAddress = metadata;
    var backHost = kc["BackchannelHost"];
    if (!string.IsNullOrWhiteSpace(backHost))
        o.BackchannelHttpHandler = new KeycloakBackchannelHandler(backHost, kc.GetValue("BackchannelPort", 8080));

    var issuers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { authority, "http://127.0.0.1:8088/realms/procurement", "http://keycloak:8080/realms/procurement" };
    if (!string.IsNullOrWhiteSpace(kc["InternalIssuer"]))
        issuers.Add(kc["InternalIssuer"]!);

    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = false,
        ValidateIssuer = true,
        ValidIssuers = issuers,
        NameClaimType = "preferred_username",
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.FromMinutes(5),
    };
    o.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Jwt")
                .LogWarning(ctx.Exception, "JWT rejected: {Message}", ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            JwtRoleClaims.CopyToIdentity(ctx);
            return Task.CompletedTask;
        },
    };
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
