using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Matching.Clients;
using ProcurementSystem.Services.Matching.Matching;

namespace ProcurementSystem.Services.Matching;

public static class DependencyInjection
{
    public static IServiceCollection AddMatchingInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<AuthForwardHandler>();
        services.AddHttpClient<ICatalogClient, CatalogClient>(http =>
        {
            var raw = config["Neighbors:Catalog:BaseUrl"] ?? "http://catalog:8080";
            http.BaseAddress = new Uri(raw.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromMinutes(10);
        }).AddHttpMessageHandler<AuthForwardHandler>();

        services.AddHttpClient<IQuotingClient, QuotingClient>(http =>
        {
            var raw = config["Neighbors:Quoting:BaseUrl"] ?? "http://quoting:8080";
            http.BaseAddress = new Uri(raw.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromMinutes(10);
        }).AddHttpMessageHandler<AuthForwardHandler>();

        services.AddSingleton<MatchingModelCache>();
        services.AddScoped<IRelevanceMatcher, RelevanceMatcher>();
        services.AddScoped<IMatchingService, MatchingService>();

        return services;
    }
}
