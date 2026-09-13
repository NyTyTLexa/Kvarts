using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Infrastructure.Pricing;

namespace ProcurementSystem.Tests;

public class DiscountServiceTests
{
    [Fact]
    public async Task Expired_discount_is_not_returned()
    {
        using var db = TestDb.Create();
        var vendorId = Guid.NewGuid();
        db.Discounts.Add(new Discount
        {
            VendorId = vendorId,
            Percent = 15m,
            ValidFromUtc = DateTime.UtcNow.AddDays(-30),
            ValidToUtc = DateTime.UtcNow.AddDays(-1) // истекла вчера
        });
        await db.SaveChangesAsync();

        var active = await new DiscountService(db).GetActiveForAsync([vendorId], []);

        Assert.Empty(active);
    }

    [Fact]
    public async Task Active_unbounded_discount_is_returned()
    {
        using var db = TestDb.Create();
        var vendorId = Guid.NewGuid();
        db.Discounts.Add(new Discount { VendorId = vendorId, Percent = 10m }); // бессрочная
        await db.SaveChangesAsync();

        var active = await new DiscountService(db).GetActiveForAsync([vendorId], []);

        var d = Assert.Single(active);
        Assert.Equal(10m, d.Percent);
    }
}
