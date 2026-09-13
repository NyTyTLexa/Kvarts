using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Domain.Messaging;
using ProcurementSystem.Services.Logistics.Integration;
using ProcurementSystem.Services.Logistics.Logistics;
using ProcurementSystem.Services.Logistics.Messaging;
using ProcurementSystem.Services.Logistics.Ordering;
using ProcurementSystem.Services.Logistics.Persistence;

namespace ProcurementSystem.Services.Logistics;

public static class DependencyInjection
{
    public static IServiceCollection AddLogisticsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<LogisticsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Postgres"), npg =>
                    npg.MigrationsHistoryTable("__EFMigrationsHistory", "logistics"))
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        var natsUrl = config["Nats:Url"] ?? "nats://localhost:4222";
        services.AddSingleton(new NatsConnection(new NatsOpts { Url = natsUrl }));
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());
        services.AddSingleton<INatsJSContext>(sp =>
            new NatsJSContext(sp.GetRequiredService<NatsConnection>()));
        services.AddScoped<IEventBus, NatsEventBus>();

        services.AddSingleton<IAccountingGateway>(sp =>
        {
            var mode = config["ExternalAccounting:Mode"] ?? "Mock";
            return mode.Equals("ErpNext", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<ErpNextAccountingGateway>(sp)
                : ActivatorUtilities.CreateInstance<MockAccountingGateway>(sp);
        });
        services.AddSingleton<IWarehouseGateway, MockWarehouseGateway>();
        services.AddScoped<IWarehouseService, WarehouseService>();

        services.AddHttpContextAccessor();
        services.AddHttpClient<IOrderReader, OrderHttpClient>((sp, http) =>
        {
            var baseUrl = sp.GetRequiredService<IConfiguration>()["Services:Ordering"]
                          ?? "http://localhost:5171";
            http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
