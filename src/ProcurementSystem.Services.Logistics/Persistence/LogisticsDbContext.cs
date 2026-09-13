using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Domain.Outbox;

namespace ProcurementSystem.Services.Logistics.Persistence;

public class LogisticsDbContext(DbContextOptions<LogisticsDbContext> options) : DbContext(options)
{
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("logistics");

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.Property(o => o.Type).HasMaxLength(300).IsRequired();
            e.HasIndex(o => o.ProcessedAtUtc);
        });

        // GoodsReceipt / GoodsReceiptLine — IEntityTypeConfiguration в этой сборке.
        // Ссылки на товар/заказ — голый Guid, без FK и без Include чужих сущностей.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogisticsDbContext).Assembly);
    }
}
