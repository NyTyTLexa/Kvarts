using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using ProcurementSystem.Services.Commercial.Commercial;
using ProcurementSystem.Services.Commercial.Neighbors;
using ProcurementSystem.Services.Commercial.Persistence;

namespace ProcurementSystem.Services.Commercial;

public static class DependencyInjection
{
    public static IServiceCollection AddCommercialInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CommercialDbContext>(opt =>
            opt.UseNpgsql(
                    config.GetConnectionString("Postgres"),
                    npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "commercial"))
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddHttpContextAccessor();
        services.AddTransient<ForwardAuthorizationHandler>();
        services.AddHttpClient<IQuotingClient, QuotingClient>((sp, http) =>
        {
            var baseUrl = config["Neighbors:QuotingBaseUrl"] ?? "http://localhost:5170";
            http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(60);
        }).AddHttpMessageHandler<ForwardAuthorizationHandler>();

        services.AddScoped<IApprovalQueryService, ApprovalQueryService>();
        services.AddScoped<IApprovalCommandService, ApprovalCommandService>();
        services.AddScoped<IInvoiceQueryService, InvoiceQueryService>();
        services.AddScoped<IInvoiceCommandService, InvoiceCommandService>();
        services.AddScoped<GoodsReceiptCompletedHandler>();

        var natsUrl = config["Nats:Url"] ?? "nats://localhost:4222";
        services.AddSingleton(new NatsConnection(new NatsOpts { Url = natsUrl }));
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());
        services.AddSingleton<INatsJSContext>(sp =>
            new NatsJSContext(sp.GetRequiredService<NatsConnection>()));

        return services;
    }
}
