using Microsoft.Extensions.DependencyInjection;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>
/// DI-регистрации модуля «Коммерческий контур» (ТРЕК A: согласование КБ + счёт NOC).
/// Этот файл — точка подключения трека: сервисы регистрируются здесь и подхватываются
/// из AddInfrastructure. Править общий DependencyInjection.cs НЕ нужно.
/// </summary>
public static class CommercialModule
{
    public static IServiceCollection AddCommercial(this IServiceCollection services)
    {
        // CQRS: чтение и мутации каждого агрегата — отдельные сервисы.
        services.AddScoped<IApprovalQueryService, ApprovalQueryService>();
        services.AddScoped<IApprovalCommandService, ApprovalCommandService>();
        services.AddScoped<IInvoiceQueryService, InvoiceQueryService>();
        services.AddScoped<IInvoiceCommandService, InvoiceCommandService>();
        // Старые фасады: WarehouseService / тесты / leftover-конструкторы ещё могут просить их.
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        return services;
    }
}
