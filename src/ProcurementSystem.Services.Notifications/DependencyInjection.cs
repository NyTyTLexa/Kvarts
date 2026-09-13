using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Services.Notifications.Email;
using ProcurementSystem.Services.Notifications.Integration;
using ProcurementSystem.Services.Notifications.Neighbors;
using ProcurementSystem.Services.Notifications.Notifications;
using ProcurementSystem.Services.Notifications.Persistence;

namespace ProcurementSystem.Services.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<NotificationsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Postgres"), npg =>
                    npg.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        var natsUrl = config["Nats:Url"] ?? "nats://localhost:4222";
        services.AddSingleton(new NatsConnection(new NatsOpts { Url = natsUrl }));
        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());

        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddSingleton<IEmailSender>(sp =>
        {
            var enabled = sp.GetRequiredService<IOptions<EmailOptions>>().Value.Enabled;
            return enabled
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp)
                : ActivatorUtilities.CreateInstance<NoOpEmailSender>(sp);
        });
        services.AddScoped<EmailNotificationProcessor>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOpenWorkSource, HttpOpenWorkSource>();

        services.AddHttpContextAccessor();
        services.AddSingleton<ServiceTokenProvider>();
        services.AddTransient<NeighborAuthHandler>();
        services.AddHttpClient(ServiceTokenProvider.HttpClientName, http =>
            http.Timeout = TimeSpan.FromSeconds(15));

        AddNeighbor(services, "commercial", "Commercial", "http://localhost:5169");
        AddNeighbor(services, "ordering", "Ordering", "http://localhost:5171");
        AddNeighbor(services, "logistics", "Logistics", "http://localhost:5172");

        return services;
    }

    static void AddNeighbor(IServiceCollection services, string name, string section, string defaultUrl)
    {
        services.AddHttpClient(name, (sp, http) =>
        {
            var baseUrl = sp.GetRequiredService<IConfiguration>()[$"Neighbors:{section}:BaseUrl"] ?? defaultUrl;
            http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(15);
        }).AddHttpMessageHandler<NeighborAuthHandler>();
    }
}
