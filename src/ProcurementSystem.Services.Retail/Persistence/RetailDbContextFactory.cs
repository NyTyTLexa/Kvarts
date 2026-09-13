using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProcurementSystem.Services.Retail.Persistence;

/// <summary>Нужен <c>dotnet ef migrations</c>, чтобы не поднимать веб-хост и JWT.</summary>
public sealed class RetailDbContextFactory : IDesignTimeDbContextFactory<RetailDbContext>
{
    public RetailDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=procurement;Username=procurement;Password=procurement",
                npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "retail"))
            .Options;
        return new RetailDbContext(options);
    }
}
