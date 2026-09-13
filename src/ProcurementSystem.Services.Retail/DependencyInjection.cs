using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Services.Retail.Clients;
using ProcurementSystem.Services.Retail.Persistence;
using ProcurementSystem.Services.Retail.Retail;

namespace ProcurementSystem.Services.Retail;

public static class DependencyInjection
{
    public static IServiceCollection AddRetailInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddDbContext<RetailDbContext>(opt =>
            opt.UseNpgsql(
                    config.GetConnectionString("Postgres"),
                    npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "retail"))
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddTransient<AuthForwardHandler>();
        services.AddHttpClient<ICatalogClient, CatalogClient>(http =>
        {
            var raw = config["Neighbors:Catalog:BaseUrl"] ?? "http://catalog:8080";
            http.BaseAddress = new Uri(raw.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromMinutes(2);
        }).AddHttpMessageHandler<AuthForwardHandler>();

        services.AddSingleton(_ => RetailHttp.Create());
        services.AddScoped<RetailHttpClient>();
        services.AddScoped<WildberriesCatalogClient>();
        services.AddScoped<DuckDuckGoDiscovery>();
        services.AddScoped<IRetailSearchService, RetailSearchService>();
        services.AddScoped<IRetailImportService, RetailImportService>();

        return services;
    }
}
