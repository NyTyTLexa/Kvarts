using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ProcurementSystem.Services.Ordering;
using ProcurementSystem.Services.Ordering.Auth;
using ProcurementSystem.Services.Ordering.Clients;
using ProcurementSystem.Services.Ordering.Observability;
using ProcurementSystem.Services.Ordering.Persistence;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("procurement-ordering");

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
        Description = "JWT access-token из Keycloak (без префикса 'Bearer ')."
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
    });
});

builder.Services.AddDbContext<OrderingDbContext>(opt =>
    opt.UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ordering"))
        .ConfigureWarnings(w => w.Ignore(
            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthForwardHandler>();
builder.Services.AddHttpClient<IQuoteClient, QuoteClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Neighbors:Quoting:BaseUrl"] ?? "http://localhost:5170");
}).AddHttpMessageHandler<AuthForwardHandler>();
builder.Services.AddHttpClient<ICatalogStockClient, CatalogStockClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Neighbors:Catalog:BaseUrl"] ?? "http://localhost:5165");
}).AddHttpMessageHandler<AuthForwardHandler>();
builder.Services.AddScoped<IOrderService, OrderService>();

var kc = builder.Configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => ConfigureKeycloakJwt(o, kc));

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("read", p => p.RequireRole("admin", "manager", "viewer", "commercial", "accounting", "warehouse"));
    o.AddPolicy("write", p => p.RequireRole("admin", "manager"));
    o.AddPolicy("admin", p => p.RequireRole("admin"));
    o.AddPolicy("approve", p => p.RequireRole("admin", "commercial"));
    o.AddPolicy("postPayment", p => p.RequireRole("admin", "accounting"));
    o.AddPolicy("receive", p => p.RequireRole("admin", "warehouse"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    db.Database.Migrate();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(o => o.RouteTemplate = "/openapi/{documentName}.json");
}

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
if (urls.Contains("https", StringComparison.OrdinalIgnoreCase))
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(o =>
    {
        o.WithTitle("Procurement Ordering API");
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
