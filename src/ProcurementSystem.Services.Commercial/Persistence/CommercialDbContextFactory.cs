using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProcurementSystem.Services.Commercial.Persistence;

/// <summary>Нужен <c>dotnet ef migrations</c>, чтобы не поднимать веб-хост и JWT.</summary>
public sealed class CommercialDbContextFactory : IDesignTimeDbContextFactory<CommercialDbContext>
{
    public CommercialDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CommercialDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=procurement;Username=procurement;Password=procurement",
                npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "commercial"))
            .Options;
        return new CommercialDbContext(options);
    }
}
