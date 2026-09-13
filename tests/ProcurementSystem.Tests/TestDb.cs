using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Tests;

/// <summary>Изолированный in-memory AppDbContext на тест — своя БД под каждый вызов.</summary>
internal static class TestDb
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
