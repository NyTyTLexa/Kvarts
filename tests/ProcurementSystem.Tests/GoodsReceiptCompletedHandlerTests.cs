using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Services.Commercial.Commercial;
using ProcurementSystem.Services.Commercial.Neighbors;
using ProcurementSystem.Services.Commercial.Persistence;

namespace ProcurementSystem.Tests;

/// <summary>
/// Обработчик GoodsReceiptCompleted без NATS: счёт «ОжиданиеПоставки» доходит до
/// «ОтраженоВ1С»; повтор и отсутствие счёта не ошибка.
/// </summary>
public class GoodsReceiptCompletedHandlerTests
{
    [Fact]
    public async Task Waiting_invoice_advances_to_posted_in_1c()
    {
        using var db = CreateDb();
        var (specId, invoiceId) = await SeedInvoiceAsync(db, InvoiceStatus.ОжиданиеПоставки);

        await CreateHandler(db).HandleAsync(Evt(specId));

        Assert.Equal(InvoiceStatus.ОтраженоВ1С, (await db.Invoices.FindAsync(invoiceId))!.Status);
    }

    [Fact]
    public async Task Replay_is_idempotent()
    {
        using var db = CreateDb();
        var (specId, invoiceId) = await SeedInvoiceAsync(db, InvoiceStatus.ОжиданиеПоставки);
        var handler = CreateHandler(db);
        var evt = Evt(specId);

        await handler.HandleAsync(evt);
        await handler.HandleAsync(evt);

        Assert.Equal(InvoiceStatus.ОтраженоВ1С, (await db.Invoices.FindAsync(invoiceId))!.Status);
    }

    [Fact]
    public async Task Missing_invoice_is_not_an_error()
    {
        using var db = CreateDb();
        var handler = CreateHandler(db);

        await handler.HandleAsync(Evt(Guid.NewGuid()));
        await handler.HandleAsync(Evt(null));
    }

    [Fact]
    public async Task Partial_advance_resumes_from_arrived_at_warehouse()
    {
        using var db = CreateDb();
        var (specId, invoiceId) = await SeedInvoiceAsync(db, InvoiceStatus.ПришёлНаСклад);

        await CreateHandler(db).HandleAsync(Evt(specId));

        Assert.Equal(InvoiceStatus.ОтраженоВ1С, (await db.Invoices.FindAsync(invoiceId))!.Status);
    }

    private static CommercialDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CommercialDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GoodsReceiptCompletedHandler CreateHandler(CommercialDbContext db) =>
        new(db, new InvoiceCommandService(db, new NoQuoting()), NullLogger<GoodsReceiptCompletedHandler>.Instance);

    private static async Task<(Guid SpecId, Guid InvoiceId)> SeedInvoiceAsync(
        CommercialDbContext db, InvoiceStatus status)
    {
        var specId = Guid.NewGuid();
        var approval = new Approval
        {
            SpecificationId = specId,
            Strategy = "MinCost",
            Title = "Спецификация для приёмки",
            Status = ApprovalStatus.Согласовано
        };
        var invoice = new Invoice
        {
            Number = $"NOC-2026-{status:D}",
            ApprovalId = approval.Id,
            Status = status
        };
        db.Approvals.Add(approval);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return (specId, invoice.Id);
    }

    private static GoodsReceiptCompleted Evt(Guid? specId) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        specId,
        "ORD-1",
        "Клиент",
        DateTime.UtcNow,
        DateTime.UtcNow,
        100m,
        "WMS-1",
        "1C-1",
        Array.Empty<GoodsReceiptCompletedLine>());

    private sealed class NoQuoting : IQuotingClient
    {
        public Task<NeighborSpecification?> GetSpecificationAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<NeighborSpecification?>(null);

        public Task<NeighborQuote?> GenerateQuoteAsync(Guid specificationId, QuoteStrategy strategy, CancellationToken ct = default)
            => Task.FromResult<NeighborQuote?>(null);
    }
}
