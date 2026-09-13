using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Services.Notifications.Notifications;
using ProcurementSystem.Services.Notifications.Persistence;

namespace ProcurementSystem.Tests;

public class NotificationsServiceTests
{
    [Fact]
    public async Task List_syncs_open_work_without_computed_properties()
    {
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var source = new FakeOpenWorkSource(
            new OpenWorkItem("approval", "commercial", "КП ожидает согласования", "Щит ТП-3: требуется решение коммерческого блока.", "Approval", Guid.NewGuid(), now),
            new OpenWorkItem("invoice", "accounting", "Счет NOC требует внимания", "NOC-2026-001: текущий статус ОжиданиеОплаты.", "Invoice", Guid.NewGuid(), now),
            new OpenWorkItem("order", "manager", "Заказ в работе", "ORD-1: Поставка, статус Confirmed.", "Order", Guid.NewGuid(), now),
            new OpenWorkItem("warehouse", "warehouse", "Приемка ожидает проведения", "Заказ ORD-1: нужно сверить складскую приемку.", "GoodsReceipt", Guid.NewGuid(), now));

        var svc = new NotificationService(db, source);
        var forManager = await svc.ListAsync("manager", ["manager"], false);
        var countCommercial = await svc.CountAsync("kb", ["commercial"]);

        Assert.Contains(forManager, n => n.Type == "order");
        Assert.DoesNotContain(forManager, n => n.Type == "approval");
        Assert.True(countCommercial.Unread >= 1);
    }

    [Fact]
    public async Task Admin_sees_every_role_inbox()
    {
        using var db = CreateDb();
        var source = new FakeOpenWorkSource(
            new OpenWorkItem("approval", "commercial", "КП ожидает согласования", "КП: требуется решение коммерческого блока.", "Approval", Guid.NewGuid(), DateTime.UtcNow));

        var list = await new NotificationService(db, source).ListAsync("admin", ["admin"], false);
        Assert.Contains(list, n => n.Type == "approval");
    }

    private static NotificationsDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FakeOpenWorkSource(params OpenWorkItem[] items) : IOpenWorkSource
    {
        public Task<IReadOnlyList<OpenWorkItem>> GetOpenWorkAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OpenWorkItem>>(items);
    }
}
