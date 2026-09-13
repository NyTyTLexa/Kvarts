using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProcurementSystem.Services.Notifications.Persistence;

/// <summary>Нужен <c>dotnet ef migrations</c>, чтобы не поднимать веб-хост и JWT.</summary>
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=procurement;Username=procurement;Password=procurement",
                npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options;
        return new NotificationsDbContext(options);
    }
}
