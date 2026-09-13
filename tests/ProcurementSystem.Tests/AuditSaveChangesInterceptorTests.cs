using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Audit;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Import;
using ProcurementSystem.Domain.Notifications;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Domain.Outbox;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Infrastructure.Audit;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Tests;

public class AuditSaveChangesInterceptorTests
{
    [Fact]
    public async Task Discount_percent_change_records_old_and_new_values()
    {
        using var db = CreateDb();
        var discount = new Discount { Manufacturer = "Acme", Percent = 10m };
        db.Discounts.Add(discount);
        await db.SaveChangesAsync();
        await ClearAuditAsync(db);

        discount.Percent = 15.5m;
        await db.SaveChangesAsync();

        var change = Assert.Single(ReadChanges(Assert.Single(db.AuditEntries)));
        Assert.Equal("Discount", change.Entity);
        Assert.Equal("Percent", change.Property);
        Assert.Equal(discount.Id.ToString(), change.EntityId);
        Assert.Equal("10", change.OldValue);
        Assert.Equal("15.5", change.NewValue);
    }

    [Fact]
    public async Task Order_status_change_records_old_and_new_statuses()
    {
        using var db = CreateDb();
        var order = new Order
        {
            Number = "ORD-AUDIT",
            Title = "Аудит",
            Strategy = "Balanced",
            Status = OrderStatus.Draft
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await ClearAuditAsync(db);

        order.Status = OrderStatus.Placed;
        await db.SaveChangesAsync();

        var change = Assert.Single(ReadChanges(Assert.Single(db.AuditEntries)));
        Assert.Equal("Order", change.Entity);
        Assert.Equal("Status", change.Property);
        Assert.Equal("Draft", change.OldValue);
        Assert.Equal("Placed", change.NewValue);
    }

    [Fact]
    public async Task Binary_and_technical_entities_are_not_audited()
    {
        using var db = CreateDb();
        var vendor = new Vendor { Name = "Поставщик" };
        db.PriceImportUploads.Add(new PriceImportUpload
        {
            Vendor = vendor,
            VendorName = vendor.Name,
            FileName = "price.xlsx",
            FileContent = [1, 2, 3]
        });
        db.OutboxMessages.Add(new OutboxMessage { Type = "Test", Payload = "{}" });
        db.Notifications.Add(new Notification
        {
            Type = "Test",
            Title = "Тест",
            Message = "Техническая запись",
            DedupeKey = Guid.NewGuid().ToString()
        });
        db.AuditEntries.Add(new AuditEntry
        {
            Action = "TEST",
            Path = "/test",
            StatusCode = 200
        });

        await db.SaveChangesAsync();

        Assert.DoesNotContain(db.AuditEntries, a => a.ChangesJson is not null);
    }

    private static AppDbContext CreateDb()
    {
        var request = new AuditRequestContext
        {
            UserName = "tester",
            Action = "PUT",
            Path = "/api/test",
            OccurredAtUtc = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(request))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task ClearAuditAsync(AppDbContext db)
    {
        db.AuditEntries.RemoveRange(db.AuditEntries);
        await db.SaveChangesAsync();
    }

    private static IReadOnlyList<AuditChange> ReadChanges(AuditEntry entry) =>
        JsonSerializer.Deserialize<List<AuditChange>>(entry.ChangesJson!, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
}
