using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Infrastructure.Integration;

namespace ProcurementSystem.Infrastructure.Logistics;

public static class LogisticsModule
{
    public static IServiceCollection AddLogistics(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IAccountingGateway>(sp =>
        {
            var mode = config["ExternalAccounting:Mode"] ?? "Mock";
            return mode.Equals("ErpNext", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<ErpNextAccountingGateway>(sp)
                : ActivatorUtilities.CreateInstance<MockAccountingGateway>(sp);
        });

        services.AddSingleton<IWarehouseGateway, MockWarehouseGateway>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        return services;
    }
}
