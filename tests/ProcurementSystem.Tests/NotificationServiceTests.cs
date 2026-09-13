using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Infrastructure.Notifications;

namespace ProcurementSystem.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task List_syncs_open_work_without_computed_properties()
    {
        using var db = TestDb.Create();
        db.Set<Approval>().Add(new Approval
        {
            Title = "Щит ТП-3",
            Strategy = "Balanced",
            Status = ApprovalStatus.ВКоммерческомБлоке,
        });
        db.Set<Invoice>().Add(new Invoice
        {
            Number = "NOC-2026-001",
            ApprovalId = Guid.NewGuid(),
            Status = InvoiceStatus.ОжиданиеОплаты,
        });
        db.Orders.Add(new Order
        {
            Number = "ORD-1",
            Title = "Поставка",
            Strategy = "Balanced",
            Status = OrderStatus.Confirmed,
        });
        db.Set<GoodsReceipt>().Add(new GoodsReceipt
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-1",
            Status = ReceiptStatus.Черновик,
        });
        await db.SaveChangesAsync();

        var svc = new NotificationService(db);
        var forManager = await svc.ListAsync("manager", ["manager"], false);
        var countCommercial = await svc.CountAsync("kb", ["commercial"]);

        Assert.Contains(forManager, n => n.Type == "order");
        Assert.DoesNotContain(forManager, n => n.Type == "approval");
        Assert.True(countCommercial.Unread >= 1);
    }

    [Fact]
    public async Task Admin_sees_every_role_inbox()
    {
        using var db = TestDb.Create();
        db.Set<Approval>().Add(new Approval
        {
            Title = "КП",
            Strategy = "MinCost",
            Status = ApprovalStatus.НаСогласованииРП,
        });
        await db.SaveChangesAsync();

        var list = await new NotificationService(db).ListAsync("admin", ["admin"], false);
        Assert.Contains(list, n => n.Type == "approval");
    }
}
