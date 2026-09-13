using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using ProcurementSystem.Domain.Messaging;
using ProcurementSystem.Services.Catalog.Catalog;
using ProcurementSystem.Services.Catalog.Import;
using ProcurementSystem.Services.Catalog.Messaging;
using ProcurementSystem.Services.Catalog.Persistence;
using ProcurementSystem.Services.Catalog.Pricing;
using ProcurementSystem.Services.Catalog.Seeding;

namespace ProcurementSystem.Services.Catalog;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<CatalogDbContext>((sp, opt) =>
            opt.UseNpgsql(
                    config.GetConnectionString("Postgres"),
                    npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>())
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        var natsUrl = config["Nats:Url"] ?? "nats://localhost:4222";
        services.AddSingleton(new NatsConnection(new NatsOpts { Url = natsUrl }));
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());
        services.AddSingleton<INatsJSContext>(sp =>
            new NatsJSContext(sp.GetRequiredService<NatsConnection>()));
        services.AddScoped<IEventBus, NatsEventBus>();

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICatalogBrowseService, CatalogBrowseService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IManufacturerService, ManufacturerService>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IPriceListImporter, ExcelPriceListImporter>();
        services.AddScoped<IPriceListCorpus, PriceListCorpus>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IPriceHistoryService, PriceHistoryService>();
        services.AddScoped<IStockAdjustService, StockAdjustService>();

        return services;
    }
}
