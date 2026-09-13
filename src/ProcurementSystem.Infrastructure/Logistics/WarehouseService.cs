using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Infrastructure.Commercial;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Logistics;

public record ReceiptLineDto(Guid Id, Guid? ProductId, string? Sku, string Name,
    int OrderedQty, int ReceivedQty, int Discrepancy, decimal UnitPrice);

public record ReceiptDto(Guid Id, Guid OrderId, string OrderNumber, string? Customer,
    ReceiptStatus Status, int LinesCount, int DiscrepancyCount, string? CreatedBy,
    DateTime CreatedAtUtc, DateTime? CompletedAtUtc, string? WarehouseRef, string? AccountingRef);

public record ReceiptDetailDto(Guid Id, Guid OrderId, string OrderNumber, string? Customer,
    ReceiptStatus Status, string? CreatedBy, DateTime CreatedAtUtc, DateTime? CompletedAtUtc,
    string? WarehouseRef, string? AccountingRef, IReadOnlyList<ReceiptLineDto> Lines);

public record SetReceivedRequest(int ReceivedQty);
public record ReceiptResult(bool Found, bool Ok, string? Error, ReceiptDetailDto? Receipt);

public interface IWarehouseService
{
    /// <summary>Создать приёмку из заказа (снимок строк; ReceivedQty по умолчанию = заказанному).</summary>
    Task<ReceiptDetailDto?> CreateFromOrderAsync(Guid orderId, string? createdBy, CancellationToken ct = default);
    Task<IReadOnlyList<ReceiptDto>> ListAsync(CancellationToken ct = default);
    Task<ReceiptDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ReceiptResult> SetReceivedAsync(Guid receiptId, Guid lineId, int receivedQty, CancellationToken ct = default);
    /// <summary>Провести приёмку: зафиксировать расхождения и выгрузить во внешние системы (WMS + 1С).</summary>
    Task<ReceiptResult> CompleteAsync(Guid receiptId, CancellationToken ct = default);
}

/// <summary>
/// Приёмка заказов на склад с фиксацией расхождений (ТЗ п.B11) и выгрузкой во внешние системы
/// (WMS/1С) через шлюзы Этапа 8. Заказ только читается — модуль заказов не меняется.
/// </summary>
public class WarehouseService(AppDbContext db, IWarehouseGateway warehouse, IAccountingGateway accounting,
    IInvoiceCommandService invoices) : IWarehouseService
{
    public async Task<ReceiptDetailDto?> CreateFromOrderAsync(Guid orderId, string? createdBy, CancellationToken ct = default)
    {
        // На заказ — одна приёмка: если уже есть, вернуть её.
        var existingId = await db.Set<GoodsReceipt>().AsNoTracking()
            .Where(r => r.OrderId == orderId).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        if (existingId is Guid eid) return await GetAsync(eid, ct);

        var order = await db.Set<Order>().AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return null;

        var receipt = new GoodsReceipt
        {
            OrderId = order.Id,
            OrderNumber = order.Number,
            Customer = order.Customer,
            CreatedBy = createdBy,
            Status = ReceiptStatus.Черновик,
            Lines = order.Lines.Select(l => new GoodsReceiptLine
            {
                ProductId = l.ProductId,
                Sku = l.Sku,
                Name = l.Name,
                OrderedQty = l.Quantity,
                ReceivedQty = l.Quantity,   // по умолчанию принято полностью, оператор корректирует
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };
        db.Add(receipt);
        await db.SaveChangesAsync(ct);
        return await GetAsync(receipt.Id, ct);
    }

    public async Task<IReadOnlyList<ReceiptDto>> ListAsync(CancellationToken ct = default) =>
        await db.Set<GoodsReceipt>().AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReceiptDto(r.Id, r.OrderId, r.OrderNumber, r.Customer, r.Status,
                r.Lines.Count, r.Lines.Count(l => l.ReceivedQty != l.OrderedQty),
                r.CreatedBy, r.CreatedAtUtc, r.CompletedAtUtc, r.WarehouseRef, r.AccountingRef))
            .ToListAsync(ct);

    public async Task<ReceiptDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.Set<GoodsReceipt>().AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? null : ToDetail(r);
    }

    public async Task<ReceiptResult> SetReceivedAsync(Guid receiptId, Guid lineId, int receivedQty, CancellationToken ct = default)
    {
        var r = await db.Set<GoodsReceipt>().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return new ReceiptResult(false, false, null, null);
        if (r.Status != ReceiptStatus.Черновик)
            return new ReceiptResult(true, false, "Приёмка уже проведена или отменена — изменение недоступно", null);

        var line = r.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return new ReceiptResult(false, false, null, null);

        line.ReceivedQty = Math.Max(0, receivedQty);
        r.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new ReceiptResult(true, true, null, ToDetail(r));
    }

    public async Task<ReceiptResult> CompleteAsync(Guid receiptId, CancellationToken ct = default)
    {
        var r = await db.Set<GoodsReceipt>().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return new ReceiptResult(false, false, null, null);
        if (r.Status != ReceiptStatus.Черновик)
            return new ReceiptResult(true, false, "Приёмка уже проведена или отменена", null);

        // Этап 8: выгрузка во внешние системы через шлюзы.
        var notice = new ReceiptNotice(r.OrderNumber,
            r.Lines.Select(l => new ReceiptNoticeLine(l.Sku ?? "", l.Name, l.ReceivedQty)).ToList());
        var wms = await warehouse.RegisterReceiptAsync(notice, ct);

        var amount = r.Lines.Sum(l => l.ReceivedQty * l.UnitPrice);
        var doc = new AccountingDocument("ПриёмкаТовара", r.OrderNumber, r.Customer ?? "—", amount, DateTime.UtcNow);
        var acc = await accounting.PostDocumentAsync(doc, ct);

        r.WarehouseRef = wms.ExternalRef;
        r.AccountingRef = acc.ExternalId;
        r.Status = ReceiptStatus.Проведено;
        r.CompletedAtUtc = DateTime.UtcNow;
        r.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await AdvanceLinkedInvoiceAsync(r.OrderId, ct);
        return new ReceiptResult(true, true, null, ToDetail(r));
    }

    /// <summary>
    /// Сшивка с Треком A: если по спецификации этого заказа есть счёт NOC, ожидающий поставки,
    /// приёмка на склад продвигает его до конечных стадий ЖЦ («Пришёл на склад» → «Отражено в 1С»,
    /// ТЗ п.B7). Не найден подходящий счёт — просто пропускаем, приёмка уже проведена.
    /// </summary>
    private async Task AdvanceLinkedInvoiceAsync(Guid orderId, CancellationToken ct)
    {
        var specId = await db.Set<Order>().AsNoTracking()
            .Where(o => o.Id == orderId).Select(o => o.SpecificationId).FirstOrDefaultAsync(ct);
        if (specId is null) return;

        var invoiceId = await db.Set<Invoice>().AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.ОжиданиеПоставки)
            .Join(db.Set<Approval>().AsNoTracking().Where(a => a.SpecificationId == specId),
                i => i.ApprovalId, a => a.Id, (i, a) => i)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);
        if (invoiceId is not Guid iid) return;

        await invoices.SetStatusAsync(iid, new SetInvoiceStatusRequest(InvoiceStatus.ПришёлНаСклад), ct);
        await invoices.SetStatusAsync(iid, new SetInvoiceStatusRequest(InvoiceStatus.ОтраженоВ1С), ct);
    }

    private static ReceiptDetailDto ToDetail(GoodsReceipt r) => new(
        r.Id, r.OrderId, r.OrderNumber, r.Customer, r.Status, r.CreatedBy, r.CreatedAtUtc,
        r.CompletedAtUtc, r.WarehouseRef, r.AccountingRef,
        r.Lines.OrderBy(l => l.Name).Select(l => new ReceiptLineDto(
            l.Id, l.ProductId, l.Sku, l.Name, l.OrderedQty, l.ReceivedQty, l.Discrepancy, l.UnitPrice)).ToList());
}
