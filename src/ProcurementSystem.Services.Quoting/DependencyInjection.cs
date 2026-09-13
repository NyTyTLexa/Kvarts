using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Quoting.Clients;
using ProcurementSystem.Services.Quoting.Matching;
using ProcurementSystem.Services.Quoting.Persistence;
using ProcurementSystem.Services.Quoting.Quoting;

namespace ProcurementSystem.Services.Quoting;

public static class DependencyInjection
{
    public static IServiceCollection AddQuotingInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<QuotingDbContext>(opt =>
            opt.UseNpgsql(
                    config.GetConnectionString("Postgres"),
                    npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "quoting"))
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.Configure<QuoteOptions>(config.GetSection("Quote"));

        services.AddHttpContextAccessor();
        services.AddTransient<AuthForwardHandler>();
        services.AddHttpClient<ICatalogClient, CatalogClient>(http =>
        {
            var raw = config["Neighbors:Catalog:BaseUrl"] ?? "http://catalog:8080";
            http.BaseAddress = new Uri(raw.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromMinutes(2);
        }).AddHttpMessageHandler<AuthForwardHandler>();

        services.AddScoped<ISpecificationService, SpecificationService>();
        services.AddScoped<IQuoteGenerator, QuoteGenerator>();
        services.AddScoped<IQuoteExcelExporter, QuoteExcelExporter>();
        services.AddScoped<IQuotePdfExporter, QuotePdfExporter>();
        services.AddScoped<ISpecificationImporter, SpecificationImporter>();
        services.AddSingleton<MatchingModelCache>();
        services.AddScoped<IRelevanceMatcher, RelevanceMatcher>();
        services.AddScoped<IMatchingService, MatchingService>();

        return services;
    }
}
