using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Commercial;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Tests;

/// <summary>
/// Регрессионный тест на баг, найденный смоук-тестом Этапа 8: переход счёта должен допускать
/// только строго следующую стадию ЖЦ, а не любой прыжок вперёд.
/// </summary>
public class InvoiceServiceTests
{
    private static InvoiceCommandService CreateService(AppDbContext db) =>
        new(db, new QuoteGenerator(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m })));

    [Fact]
    public async Task Next_stage_transition_is_allowed()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-001", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.Создан };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetStatusAsync(inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.Согласован));

        Assert.True(result.Found);
        Assert.True(result.Ok);
        Assert.Equal(InvoiceStatus.Согласован, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    [Fact]
    public async Task Skipping_a_stage_is_rejected()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-002", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.Создан };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetStatusAsync(inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.ОжиданиеОплаты));

        Assert.True(result.Found);
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal(InvoiceStatus.Создан, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    // --- Построчные позиции счёта (ТЗ п.6.1, Этап 10.2) ---

    [Fact]
    public async Task Creating_invoice_snapshots_lines_from_approved_quote()
    {
        using var db = TestDb.Create();

        // Каталог: один товар 100 ₽, спецификация на 3 шт.
        var vendor = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-INV-1", Name = "Товар для счёта" };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = vendor, Price = 100m, LeadTimeDays = 5, StockQuantity = 10 });
        var spec = new Specification { Title = "Спецификация для счёта" };
        spec.Items.Add(new SpecificationItem { RawName = "Позиция", Quantity = 3, Product = product });
        db.Specifications.Add(spec);

        // Согласованное КП с наценкой 10%.
        var approval = new Approval
        {
            SpecificationId = spec.Id, Strategy = "MinCost", Title = spec.Title,
            CostPrice = 300m, MarkupPercent = 10m, SellPrice = 330m,
            Status = ApprovalStatus.Согласовано
        };
        db.Set<Approval>().Add(approval);
        await db.SaveChangesAsync();

        var (dto, error, _) = await CreateService(db).CreateAsync(new CreateInvoiceRequest(approval.Id, null, null), "test");

        Assert.Null(error);
        var line = Assert.Single(dto!.Lines);
        Assert.Equal("SKU-INV-1", line.Sku);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(100m, line.UnitCost);            // закупочная без скидки
        Assert.Equal(110m, line.UnitPrice);           // 100 × 1.10 — наценка согласования
        Assert.Equal(330m, line.LineTotal);
    }

    [Fact]
    public async Task Invoice_from_unapproved_approval_is_rejected()
    {
        using var db = TestDb.Create();
        var approval = new Approval
        {
            SpecificationId = Guid.NewGuid(), Strategy = "MinCost", Title = "Не согласовано",
            Status = ApprovalStatus.ВКоммерческомБлоке
        };
        db.Set<Approval>().Add(approval);
        await db.SaveChangesAsync();

        var (dto, error, conflict) = await CreateService(db).CreateAsync(new CreateInvoiceRequest(approval.Id, null, null), "test");

        Assert.Null(dto);
        Assert.NotNull(error);
        Assert.True(conflict);
    }

    [Fact]
    public async Task Commercial_can_set_approved_from_created()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-010", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.Создан };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetStatusAsync(
            inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.Согласован), InvoiceStatusActor.Commercial);

        Assert.True(result.Found);
        Assert.True(result.Ok);
        Assert.False(result.Forbidden);
        Assert.Equal(InvoiceStatus.Согласован, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    [Fact]
    public async Task Accounting_cannot_set_approved()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-011", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.Создан };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetStatusAsync(
            inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.Согласован), InvoiceStatusActor.Accounting);

        Assert.True(result.Found);
        Assert.False(result.Ok);
        Assert.True(result.Forbidden);
        Assert.NotNull(result.Error);
        Assert.Equal(InvoiceStatus.Создан, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    [Fact]
    public async Task Accounting_can_set_awaiting_payment_after_approved()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-012", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.Согласован };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetStatusAsync(
            inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.ОжиданиеОплаты), InvoiceStatusActor.Accounting);

        Assert.True(result.Found);
        Assert.True(result.Ok);
        Assert.False(result.Forbidden);
        Assert.Equal(InvoiceStatus.ОжиданиеОплаты, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    [Fact]
    public async Task Commercial_cannot_set_stages_after_approved()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-013", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.Согласован };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var pay = await svc.SetStatusAsync(
            inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.ОжиданиеОплаты), InvoiceStatusActor.Commercial);
        Assert.True(pay.Found);
        Assert.False(pay.Ok);
        Assert.True(pay.Forbidden);
        Assert.Equal(InvoiceStatus.Согласован, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);

        var cancel = await svc.SetStatusAsync(
            inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.Отменён), InvoiceStatusActor.Commercial);
        Assert.True(cancel.Forbidden);
        Assert.False(cancel.Ok);
        Assert.Equal(InvoiceStatus.Согласован, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    [Fact]
    public async Task System_warehouse_path_from_awaiting_delivery_to_1c()
    {
        using var db = TestDb.Create();
        var inv = new Invoice { Number = "NOC-2026-014", ApprovalId = Guid.NewGuid(), Status = InvoiceStatus.ОжиданиеПоставки };
        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var arrived = await svc.SetStatusAsync(inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.ПришёлНаСклад));
        Assert.True(arrived.Ok);
        Assert.Equal(InvoiceStatus.ПришёлНаСклад, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);

        var posted = await svc.SetStatusAsync(inv.Id, new SetInvoiceStatusRequest(InvoiceStatus.ОтраженоВ1С));
        Assert.True(posted.Ok);
        Assert.Equal(InvoiceStatus.ОтраженоВ1С, (await db.Set<Invoice>().FindAsync(inv.Id))!.Status);
    }

    [Fact]
    public async Task Creating_invoice_twice_for_same_approval_returns_existing()
    {
        using var db = TestDb.Create();

        var vendor = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-INV-1", Name = "Товар для счёта" };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = vendor, Price = 100m, LeadTimeDays = 5, StockQuantity = 10 });
        var spec = new Specification { Title = "Спецификация для счёта" };
        spec.Items.Add(new SpecificationItem { RawName = "Позиция", Quantity = 3, Product = product });
        db.Specifications.Add(spec);

        var approval = new Approval
        {
            SpecificationId = spec.Id, Strategy = "MinCost", Title = spec.Title,
            CostPrice = 300m, MarkupPercent = 10m, SellPrice = 330m,
            Status = ApprovalStatus.Согласовано
        };
        db.Set<Approval>().Add(approval);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var (first, err1, conflict1) = await svc.CreateAsync(new CreateInvoiceRequest(approval.Id, null, null), "test");
        var (second, err2, conflict2) = await svc.CreateAsync(new CreateInvoiceRequest(approval.Id, null, null), "other");

        Assert.Null(err1);
        Assert.Null(err2);
        Assert.False(conflict1);
        Assert.False(conflict2);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Single(db.Set<Invoice>().ToList());
    }
}
