using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProcurementSystem.Services.Quoting.Persistence;

/// <summary>Нужен <c>dotnet ef migrations</c>, чтобы не поднимать веб-хост и JWT.</summary>
public sealed class QuotingDbContextFactory : IDesignTimeDbContextFactory<QuotingDbContext>
{
    public QuotingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QuotingDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=procurement;Username=procurement;Password=procurement",
                npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "quoting"))
            .Options;
        return new QuotingDbContext(options);
    }
}
