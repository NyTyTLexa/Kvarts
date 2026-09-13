using Meilisearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Domain.Messaging;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Integration;
using ProcurementSystem.Infrastructure.Audit;
using ProcurementSystem.Infrastructure.Catalog;
using ProcurementSystem.Infrastructure.Commercial;
using ProcurementSystem.Infrastructure.Logistics;
using ProcurementSystem.Infrastructure.Import;
using ProcurementSystem.Infrastructure.Matching;
using ProcurementSystem.Infrastructure.Messaging;
using ProcurementSystem.Infrastructure.Notifications;
using ProcurementSystem.Infrastructure.Ordering;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using ProcurementSystem.Infrastructure.Projects;
using ProcurementSystem.Infrastructure.Quoting;
using ProcurementSystem.Infrastructure.Retail;
using ProcurementSystem.Infrastructure.Search;
using ProcurementSystem.Infrastructure.Seeding;

namespace ProcurementSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // PostgreSQL
        services.AddScoped<AuditRequestContext>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, opt) =>
            opt.UseNpgsql(config.GetConnectionString("Postgres"))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>())
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // Meilisearch (Р·Р° Р°Р±СЃС‚СЂР°РєС†РёРµР№ ISearchEngine)
        var meiliUrl = config["Meilisearch:Url"] ?? "http://localhost:7700";
        var meiliKey = config["Meilisearch:ApiKey"];
        services.AddSingleton(new MeilisearchClient(meiliUrl, meiliKey));
        services.AddScoped<ISearchEngine, MeilisearchSearchEngine>();
        services.AddScoped<IProductIndexer, ProductIndexer>();

        // NATS JetStream (Р·Р° Р°Р±СЃС‚СЂР°РєС†РёРµР№ IEventBus)
        var natsUrl = config["Nats:Url"] ?? "nats://localhost:4222";
        services.AddSingleton(new NatsConnection(new NatsOpts { Url = natsUrl }));
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());
        services.AddSingleton<INatsJSContext>(sp =>
            new NatsJSContext(sp.GetRequiredService<NatsConnection>()));
        services.AddScoped<IEventBus, NatsEventBus>();

        // РџСЂРёРєР»Р°РґРЅС‹Рµ СЃРµСЂРІРёСЃС‹ РєР°С‚Р°Р»РѕРіР° (Р­С‚Р°Рї 1)
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICatalogBrowseService, CatalogBrowseService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IManufacturerService, ManufacturerService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IDataSeeder, DataSeeder>();
        services.AddScoped<IPriceListCorpus, PriceListCorpus>();
        services.AddScoped<IPriceListImporter, ExcelPriceListImporter>();

        services.AddSingleton(_ => RetailHttp.Create());
        services.AddScoped<RetailHttpClient>();
        services.AddScoped<WildberriesCatalogClient>();
        services.AddScoped<DuckDuckGoDiscovery>();
        services.AddScoped<IRetailSearchService, RetailSearchService>();
        services.AddScoped<IRetailImportService, RetailImportService>();

        // Р“РµРЅРµСЂР°С†РёСЏ РљРџ (Р­С‚Р°Рї 3) + СЃРєРёРґРєРё Рё РќР”РЎ (РўР— Рї.5.4)
        services.Configure<QuoteOptions>(config.GetSection("Quote"));
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<ISpecificationService, SpecificationService>();
        services.AddScoped<IQuoteGenerator, QuoteGenerator>();
        services.AddScoped<IQuoteExcelExporter, QuoteExcelExporter>();
        services.AddScoped<IQuotePdfExporter, QuotePdfExporter>();
        services.AddScoped<ISpecificationImporter, SpecificationImporter>();
        services.AddSingleton<MatchingModelCache>();
        services.AddScoped<IRelevanceMatcher, RelevanceMatcher>();
        services.AddScoped<IMatchingService, MatchingService>();

        // Р—Р°РєР°Р·С‹, РёСЃС‚РѕСЂРёСЏ С†РµРЅ, Р°СѓРґРёС‚ (Р­С‚Р°Рї 5)
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPriceHistoryService, PriceHistoryService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Email (SMTP / MailHog). Enabled=false → no-op, стенд без почты не падает.
        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddSingleton<IEmailSender>(sp =>
        {
            var enabled = sp.GetRequiredService<IOptions<EmailOptions>>().Value.Enabled;
            return enabled
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp)
                : ActivatorUtilities.CreateInstance<NoOpEmailSender>(sp);
        });
        services.AddScoped<EmailNotificationProcessor>();

        // Р РµС‚РµРЅС†РёСЏ Р¶СѓСЂРЅР°Р»Р° Р°СѓРґРёС‚Р° (РўР—: ~3 РіРѕРґР°) вЂ” РЅР°СЃС‚СЂРѕР№РєРё РґР»СЏ С„РѕРЅРѕРІРѕРіРѕ СЃРµСЂРІРёСЃР° РѕС‡РёСЃС‚РєРё
        services.Configure<AuditOptions>(config.GetSection("Audit"));

        // РњРѕРґСѓР»Рё РїР°СЂР°Р»Р»РµР»СЊРЅС‹С… С‚СЂРµРєРѕРІ вЂ” СЂРµРіРёСЃС‚СЂР°С†РёРё Р¶РёРІСѓС‚ РІРЅСѓС‚СЂРё СЃР°РјРёС… РјРѕРґСѓР»РµР№.
        services.AddCommercial();   // РўР Р•Рљ A: СЃРѕРіР»Р°СЃРѕРІР°РЅРёРµ РљР‘ + СЃС‡С‘С‚ NOC (Cline)
        services.AddLogistics(config);    // РўР Р•Рљ B: РїСЂРёС‘РјРєР° СЃРєР»Р°РґР° + РёРЅС‚РµРіСЂР°С†РёРё 1РЎ/WMS (Р­С‚Р°Рї 8)

        return services;
    }
}


