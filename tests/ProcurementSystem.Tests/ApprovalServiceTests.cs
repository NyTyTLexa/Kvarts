using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Commercial;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Tests;

/// <summary>
/// Закрепляет возврат отклонённого согласования на доработку (ТЗ п.5.6, Этап 10.5):
/// «при отклонении процесс возвращается на шаг назад с сохранением комментария».
/// </summary>
public class ApprovalServiceTests
{
    private static ApprovalCommandService CreateService(AppDbContext db)
    {
        var quotes = new QuoteGenerator(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m }));
        return new ApprovalCommandService(db, quotes, new InvoiceCommandService(db, quotes));
    }

    private static Approval Rejected() => new()
    {
        SpecificationId = Guid.NewGuid(),
        Strategy = "Balanced",
        Title = "Тестовое согласование",
        Status = ApprovalStatus.Отклонено,
        Comment = "Цена завышена",
        DecidedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task Resubmit_rejected_returns_to_first_stage_keeping_comment()
    {
        using var db = TestDb.Create();
        var a = Rejected();
        db.Set<Approval>().Add(a);
        await db.SaveChangesAsync();

        var (found, error) = await CreateService(db).ResubmitAsync(a.Id);

        Assert.True(found);
        Assert.Null(error);
        var reloaded = (await db.Set<Approval>().FindAsync(a.Id))!;
        Assert.Equal(ApprovalStatus.НаСогласованииРП, reloaded.Status);
        Assert.Null(reloaded.DecidedAtUtc);
        Assert.Equal("Цена завышена", reloaded.Comment); // комментарий отклонения не затирается
    }

    [Fact]
    public async Task Resubmit_non_rejected_is_refused()
    {
        using var db = TestDb.Create();
        var a = Rejected();
        a.Status = ApprovalStatus.ВКоммерческомБлоке;
        db.Set<Approval>().Add(a);
        await db.SaveChangesAsync();

        var (found, error) = await CreateService(db).ResubmitAsync(a.Id);

        Assert.True(found);
        Assert.NotNull(error);
        Assert.Equal(ApprovalStatus.ВКоммерческомБлоке, (await db.Set<Approval>().FindAsync(a.Id))!.Status);
    }

    [Fact]
    public async Task Resubmit_missing_approval_reports_not_found()
    {
        using var db = TestDb.Create();

        var (found, _) = await CreateService(db).ResubmitAsync(Guid.NewGuid());

        Assert.False(found);
    }

    [Fact]
    public async Task Decide_approved_creates_exactly_one_auto_invoice_with_quote_lines()
    {
        using var db = TestDb.Create();
        SeedCatalogAndApproval(db, approved: false, out var approval);
        await db.SaveChangesAsync();

        var (found, error) = await CreateService(db).DecideAsync(approval.Id, new DecisionRequest(true, null));

        Assert.True(found);
        Assert.Null(error);
        var inv = Assert.Single(await db.Set<Invoice>().Include(i => i.Lines).ToListAsync());
        Assert.Equal(approval.Id, inv.ApprovalId);
        Assert.Equal(Invoice.AutoCreatedBy, inv.CreatedBy);

        var line = Assert.Single(inv.Lines);
        Assert.Equal("SKU-INV-1", line.Sku);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(100m, line.UnitCost);
        Assert.Equal(110m, line.UnitPrice);
        Assert.Equal(330m, line.LineTotal);
    }

    [Fact]
    public async Task Manual_create_after_auto_invoice_returns_same_invoice()
    {
        using var db = TestDb.Create();
        SeedCatalogAndApproval(db, approved: false, out var approval);
        await db.SaveChangesAsync();

        var quotes = new QuoteGenerator(db, new DiscountService(db), Options.Create(new QuoteOptions { VatRate = 20m }));
        var invoices = new InvoiceCommandService(db, quotes);
        var approvals = new ApprovalCommandService(db, quotes, invoices);

        var (found, error) = await approvals.DecideAsync(approval.Id, new DecisionRequest(true, null));
        Assert.True(found);
        Assert.Null(error);

        var auto = Assert.Single(db.Set<Invoice>().ToList());
        var (dto, createError, _) = await invoices.CreateAsync(new CreateInvoiceRequest(approval.Id, null, null), "manager");

        Assert.Null(createError);
        Assert.Equal(auto.Id, dto!.Id);
        Assert.Equal(Invoice.AutoCreatedBy, dto.CreatedBy);
        Assert.Single(db.Set<Invoice>().ToList());
    }

    [Fact]
    public async Task Decide_rejected_does_not_create_invoice()
    {
        using var db = TestDb.Create();
        SeedCatalogAndApproval(db, approved: false, out var approval);
        await db.SaveChangesAsync();

        var (found, error) = await CreateService(db).DecideAsync(approval.Id, new DecisionRequest(false, "дорого"));

        Assert.True(found);
        Assert.Null(error);
        Assert.Empty(db.Set<Invoice>().ToList());
    }

    private static void SeedCatalogAndApproval(AppDbContext db, bool approved, out Approval approval)
    {
        var vendor = new Vendor { Name = "Поставщик", DefaultLeadTimeDays = 5 };
        var product = new Product { Sku = "SKU-INV-1", Name = "Товар для счёта" };
        db.Vendors.Add(vendor);
        db.Products.Add(product);
        db.PriceListItems.Add(new PriceListItem { Product = product, Vendor = vendor, Price = 100m, LeadTimeDays = 5, StockQuantity = 10 });
        var spec = new Specification { Title = "Спецификация для счёта" };
        spec.Items.Add(new SpecificationItem { RawName = "Позиция", Quantity = 3, Product = product });
        db.Specifications.Add(spec);

        approval = new Approval
        {
            SpecificationId = spec.Id, Strategy = "MinCost", Title = spec.Title,
            CostPrice = 300m, MarkupPercent = 10m, SellPrice = 330m,
            Status = approved ? ApprovalStatus.Согласовано : ApprovalStatus.ВКоммерческомБлоке
        };
        db.Set<Approval>().Add(approval);
    }
}
