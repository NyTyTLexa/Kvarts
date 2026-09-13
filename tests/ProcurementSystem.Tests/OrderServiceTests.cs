using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Ordering;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Tests;

/// <summary>Закрепляет допустимые/недопустимые переходы статуса заказа (Этап 5).</summary>
public class OrderServiceTests
{
    private static OrderService CreateService(AppDbContext db) =>
        new(db, new QuoteGenerator(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m })));

    [Fact]
    public async Task Draft_to_Placed_is_allowed()
    {
        using var db = TestDb.Create();
        var order = new Order { Number = "ORD-1", Title = "Тест", Strategy = "Balanced", Status = OrderStatus.Draft };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).ChangeStatusAsync(order.Id, OrderStatus.Placed);

        Assert.True(result.Found);
        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal(OrderStatus.Placed, (await db.Orders.FindAsync(order.Id))!.Status);
    }

    [Fact]
    public async Task Draft_to_Completed_is_rejected_as_stage_skip()
    {
        using var db = TestDb.Create();
        var order = new Order { Number = "ORD-2", Title = "Тест", Strategy = "Balanced", Status = OrderStatus.Draft };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await CreateService(db).ChangeStatusAsync(order.Id, OrderStatus.Completed);

        Assert.True(result.Found);
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal(OrderStatus.Draft, (await db.Orders.FindAsync(order.Id))!.Status);
    }

    // --- Резерв остатка (ТЗ UC-04 шаг 4, Этап 10.4) ---

    [Fact]
    public async Task Creating_order_reserves_stock()
    {
        using var db = TestDb.Create();
        var offer = await SeedOfferWithSpecAsync(db, stock: 10, orderedQty: 3);

        var order = await CreateService(db).CreateFromQuoteAsync(offer.SpecId, QuoteStrategy.MinCost, 0.5, 0.5, false, "test");

        Assert.NotNull(order);
        Assert.Equal(7, (await db.PriceListItems.FindAsync(offer.OfferId))!.StockQuantity);
    }

    [Fact]
    public async Task Cancelling_order_returns_reserved_stock()
    {
        using var db = TestDb.Create();
        var offer = await SeedOfferWithSpecAsync(db, stock: 10, orderedQty: 3);
        var svc = CreateService(db);
        var order = (await svc.CreateFromQuoteAsync(offer.SpecId, QuoteStrategy.MinCost, 0.5, 0.5, false, "test"))!;

        var result = await svc.ChangeStatusAsync(order.Id, OrderStatus.Cancelled);

        Assert.True(result.Ok);
        Assert.Equal(10, (await db.PriceListItems.FindAsync(offer.OfferId))!.StockQuantity);
    }

    [Fact]
    public async Task Reservation_never_goes_below_zero()
    {
        using var db = TestDb.Create();
        var offer = await SeedOfferWithSpecAsync(db, stock: 2, orderedQty: 5);

        var order = await CreateService(db).CreateFromQuoteAsync(offer.SpecId, QuoteStrategy.MinCost, 0.5, 0.5, false, "test");

        Assert.NotNull(order); // заказ оформляется даже при нехватке остатка (резерв не блокирующий)
        Assert.Equal(0, (await db.PriceListItems.FindAsync(offer.OfferId))!.StockQuantity);
    }

    /// <summary>Один поставщик, один товар с остатком, спецификация на orderedQty штук.</summary>
    private static async Task<(Guid SpecId, Guid OfferId)> SeedOfferWithSpecAsync(AppDbContext db, int stock, int orderedQty)
    {
        var vendor = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-STOCK-1", Name = "Товар с остатком" };
        var offer = new PriceListItem { Product = product, Vendor = vendor, Price = 100m, LeadTimeDays = 5, StockQuantity = stock };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(offer);
        var spec = new Specification { Title = "Спецификация с резервом" };
        spec.Items.Add(new SpecificationItem { RawName = "Позиция", Quantity = orderedQty, Product = product });
        db.Specifications.Add(spec);
        await db.SaveChangesAsync();
        return (spec.Id, offer.Id);
    }
}
