using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using ProcurementSystem.Api.Auditing;
using ProcurementSystem.Api.Auth;
using ProcurementSystem.Api.BackgroundServices;
using ProcurementSystem.Infrastructure;
using ProcurementSystem.Infrastructure.Observability;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Seeding;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Наблюдаемость (Этап 6): структурные логи + трассировка/метрики OpenTelemetry → Jaeger
builder.AddObservability("procurement-api");

builder.Services.AddControllers()
    // enum'ы (статусы заказа, стратегии КП) — строками в JSON, так читабельнее и принимаются по имени
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 64L * 1024 * 1024;
    o.ValueLengthLimit = int.MaxValue;
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64L * 1024 * 1024);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // Кнопка Authorize в Swagger: вставляется JWT, полученный от Keycloak.
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access-token из Keycloak (без префикса 'Bearer ')."
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
    });
});

// PostgreSQL + Meilisearch + NATS JetStream
builder.Services.AddInfrastructure(builder.Configuration);

// Фоновый процессор Outbox: публикует доменные события в NATS JetStream
builder.Services.AddHostedService<OutboxProcessor>();

// Ретенция журнала аудита: периодически удаляет записи старее Audit:RetentionDays (ТЗ: ~3 года)
builder.Services.AddHostedService<AuditRetentionService>();

// Email-уведомления: подтягивает события по ролям и шлёт SMTP (MailHog на стенде)
builder.Services.AddHostedService<EmailNotificationDispatcher>();

// --- Аутентификация (OIDC/JWT через Keycloak) ---
var kc = builder.Configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => ConfigureKeycloakJwt(o, kc));

// --- Авторизация (RBAC) ---
builder.Services.AddAuthorization(o =>
{
    // По умолчанию любой эндпоинт требует аутентификации (если не помечен AllowAnonymous).
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("read", p => p.RequireRole("admin", "manager", "viewer", "commercial", "accounting", "warehouse"));
    o.AddPolicy("write", p => p.RequireRole("admin", "manager"));          // РП: каталог/спецификации/КП/скидки (UC-01..05)
    o.AddPolicy("admin", p => p.RequireRole("admin"));
    o.AddPolicy("approve", p => p.RequireRole("admin", "commercial"));    // КБ: решение по согласованию КП (UC-06)
    o.AddPolicy("postPayment", p => p.RequireRole("admin", "accounting")); // Бухгалтерия: продвижение статуса счёта (UC-08)
    o.AddPolicy("setInvoiceStatus", p => p.RequireRole("admin", "commercial", "accounting")); // КБ: «Согласован»; бухгалтерия: последующие (10.11)
    o.AddPolicy("receive", p => p.RequireRole("admin", "warehouse"));      // Склад: приёмка на склад (UC-09)
});

var app = builder.Build();

// Применяем ожидающие миграции при старте (удобно для разработки)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (args.Any(a => string.Equals(a, "seed-corpus", StringComparison.OrdinalIgnoreCase)))
{
    using var scope = app.Services.CreateScope();
    var corpus = scope.ServiceProvider.GetRequiredService<IPriceListCorpus>();
    var result = await corpus.SeedAsync(120, 4000);
    Console.WriteLine(
        $"seed-corpus vendors={result.Vendors} products={result.Products} offers={result.Offers} " +
        $"uploads={result.Uploads} spec={result.SpecificationId} items={result.SpecItems} unmatched={result.SpecUnmatched} ms={result.ElapsedMs}");
    return;
}

// Структурный лог по каждому HTTP-запросу (метод, путь, код, длительность, TraceId)
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    // JSON для Scalar: /openapi/v1.json (не старый /swagger)
    app.UseSwagger(o => o.RouteTemplate = "/openapi/{documentName}.json");
}

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
if (urls.Contains("https", StringComparison.OrdinalIgnoreCase))
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();   // журнал действий — после аутентификации
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(o =>
    {
        o.WithTitle("Procurement System API");
        o.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
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
