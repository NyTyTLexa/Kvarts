using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Domain.Logistics;
using ProcurementSystem.Services.Logistics.Ordering;
using ProcurementSystem.Services.Logistics.Outbox;
using ProcurementSystem.Services.Logistics.Persistence;

namespace ProcurementSystem.Services.Logistics.Logistics;

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
/// (WMS/1С) через шлюзы Этапа 8. Заказ читается у соседа по HTTP; счёт больше не трогаем —
/// после проведения публикуем <see cref="GoodsReceiptCompleted"/>.
/// </summary>
public class WarehouseService(
    LogisticsDbContext db,
    IWarehouseGateway warehouse,
    IAccountingGateway accounting,
    IOrderReader orders) : IWarehouseService
{
    public async Task<ReceiptDetailDto?> CreateFromOrderAsync(Guid orderId, string? createdBy, CancellationToken ct = default)
    {
        var existingId = await db.GoodsReceipts.AsNoTracking()
            .Where(r => r.OrderId == orderId).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        if (existingId is Guid eid) return await GetAsync(eid, ct);

        var order = await orders.GetAsync(orderId, ct);
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
                ReceivedQty = l.Quantity,
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };
        db.Add(receipt);
        await db.SaveChangesAsync(ct);
        return await GetAsync(receipt.Id, ct);
    }

    public async Task<IReadOnlyList<ReceiptDto>> ListAsync(CancellationToken ct = default) =>
        await db.GoodsReceipts.AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ReceiptDto(r.Id, r.OrderId, r.OrderNumber, r.Customer, r.Status,
                r.Lines.Count, r.Lines.Count(l => l.ReceivedQty != l.OrderedQty),
                r.CreatedBy, r.CreatedAtUtc, r.CompletedAtUtc, r.WarehouseRef, r.AccountingRef))
            .ToListAsync(ct);

    public async Task<ReceiptDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? null : ToDetail(r);
    }

    public async Task<ReceiptResult> SetReceivedAsync(Guid receiptId, Guid lineId, int receivedQty, CancellationToken ct = default)
    {
        var r = await db.GoodsReceipts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return new ReceiptResult(false, false, null, null);
        if (!r.IsDraft)
            return new ReceiptResult(true, false, "Приёмка уже проведена или отменена — изменение недоступно", null);

        var line = r.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return new ReceiptResult(false, false, null, null);

        r.SetReceived(line, receivedQty);
        await db.SaveChangesAsync(ct);
        return new ReceiptResult(true, true, null, ToDetail(r));
    }

    public async Task<ReceiptResult> CompleteAsync(Guid receiptId, CancellationToken ct = default)
    {
        var r = await db.GoodsReceipts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return new ReceiptResult(false, false, null, null);
        if (!r.IsDraft)
            return new ReceiptResult(true, false, "Приёмка уже проведена или отменена", null);

        // SpecificationId нужен Commercial, чтобы найти счёт без таблицы заказов.
        // Берём до WMS/1С: падение HTTP не оставит внешние системы с проведённой приёмкой без записи.
        var specId = (await orders.GetAsync(r.OrderId, ct))?.SpecificationId;

        var notice = new ReceiptNotice(r.OrderNumber,
            r.Lines.Select(l => new ReceiptNoticeLine(l.Sku ?? "", l.Name, l.ReceivedQty)).ToList());
        var wms = await warehouse.RegisterReceiptAsync(notice, ct);

        var amount = r.ReceivedAmount;
        var doc = new AccountingDocument("ПриёмкаТовара", r.OrderNumber, r.Customer ?? "—", amount, DateTime.UtcNow);
        var acc = await accounting.PostDocumentAsync(doc, ct);

        r.Complete(wms.ExternalRef, acc.ExternalId);
        db.EnqueueEvent(new GoodsReceiptCompleted(
            r.Id,
            r.OrderId,
            specId,
            r.OrderNumber,
            r.Customer,
            r.CompletedAtUtc ?? DateTime.UtcNow,
            DateTime.UtcNow,
            amount,
            r.WarehouseRef,
            r.AccountingRef,
            r.Lines.Select(l => new GoodsReceiptCompletedLine(
                l.Id, l.ProductId, l.Sku, l.Name, l.OrderedQty, l.ReceivedQty, l.Discrepancy, l.UnitPrice)).ToList()));
        await db.SaveChangesAsync(ct);

        return new ReceiptResult(true, true, null, ToDetail(r));
    }

    private static ReceiptDetailDto ToDetail(GoodsReceipt r) => new(
        r.Id, r.OrderId, r.OrderNumber, r.Customer, r.Status, r.CreatedBy, r.CreatedAtUtc,
        r.CompletedAtUtc, r.WarehouseRef, r.AccountingRef,
        r.Lines.OrderBy(l => l.Name).Select(l => new ReceiptLineDto(
            l.Id, l.ProductId, l.Sku, l.Name, l.OrderedQty, l.ReceivedQty, l.Discrepancy, l.UnitPrice)).ToList());
}
