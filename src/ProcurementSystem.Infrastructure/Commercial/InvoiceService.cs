using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Infrastructure.Commercial;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct = default);
    Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(InvoiceDetailDto? Dto, string? Error, bool Conflict)> CreateAsync(CreateInvoiceRequest req, string? createdBy, CancellationToken ct = default);
    Task<InvoiceStatusResult> SetStatusAsync(Guid id, SetInvoiceStatusRequest req, CancellationToken ct = default);

    // Прикреплённые документы (ТЗ п.6.1) — загрузка/список/скачивание оригинала.
    Task<InvoiceAttachmentDto?> UploadAttachmentAsync(Guid invoiceId, string fileName, string contentType, byte[] content, string? uploadedBy, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceAttachmentDto>> ListAttachmentsAsync(Guid invoiceId, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, string FileName)?> GetAttachmentFileAsync(Guid attachmentId, CancellationToken ct = default);
}

/// <summary>
/// Счета NOC (ТЗ п.B7). Создаются только из согласованного Approval; сумма, наценка и
/// контрагент переносятся из согласования, позиции — снимок согласованного варианта КП
/// (тот же вызов IQuoteGenerator, что делал ApprovalService). Продвижение по стадиям ЖЦ —
/// с проверкой допустимости перехода (линейное движение вперёд + «Отменён» из любого
/// не финального; недопустимый переход → 409 Conflict).
/// </summary>
public class InvoiceService(AppDbContext db, IQuoteGenerator quotes) : IInvoiceService
{
    // Линейная цепочка ЖЦ (без терминального «Отменён»).
    private static readonly InvoiceStatus[] LinearFlow =
    [
        InvoiceStatus.Создан,
        InvoiceStatus.Согласован,
        InvoiceStatus.ОжиданиеОплаты,
        InvoiceStatus.ЧастичнаяОплата,
        InvoiceStatus.Оплачено,
        InvoiceStatus.ОжиданиеПоставки,
        InvoiceStatus.ПришёлНаСклад,
        InvoiceStatus.ОтраженоВ1С
    ];

    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct = default) =>
        // Inline-проекция (не Map): Lines.Count транслируется в подзапрос COUNT — как в OrderService.
        await db.Set<Invoice>().AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvoiceDto(
                i.Id, i.Number, i.ApprovalId, i.Customer, i.Contract,
                i.CostPrice, i.MarkupPercent, i.SellPrice, i.Status,
                i.DueDateUtc, i.CreatedBy, i.CreatedAtUtc, i.Lines.Count))
            .ToListAsync(ct);

    public async Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var inv = await db.Set<Invoice>().AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        return inv is null ? null : MapDetail(inv);
    }

    public async Task<(InvoiceDetailDto? Dto, string? Error, bool Conflict)> CreateAsync(
        CreateInvoiceRequest req, string? createdBy, CancellationToken ct = default)
    {
        // UC-07: повторное создание по тому же согласованию — тот же счёт (идемпотентно).
        var existing = await db.Set<Invoice>().Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.ApprovalId == req.ApprovalId, ct);
        if (existing is not null)
            return (MapDetail(existing), null, false);

        var approval = await db.Set<Approval>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == req.ApprovalId, ct);
        if (approval is null) return (null, "Согласование не найдено", false);
        if (approval.Status != ApprovalStatus.Согласовано)
            return (null, "Счёт можно создать только из согласованного КП", true);

        var inv = new Invoice
        {
            Number = await NextNumberAsync(ct),
            ApprovalId = approval.Id,
            Customer = approval.Customer,
            Contract = req.Contract,
            CostPrice = approval.CostPrice,
            MarkupPercent = approval.MarkupPercent,
            SellPrice = approval.SellPrice,
            Status = InvoiceStatus.Создан,
            DueDateUtc = req.DueDateUtc,
            CreatedBy = createdBy
        };

        // Позиции — снимок согласованного варианта КП (ТЗ п.6.1): тот же вызов генератора
        // с теми же параметрами по умолчанию, что в ApprovalService.CreateAsync, поэтому
        // выбор офферов (стратегия, скидки, ручные QuoteOverride) воспроизводится.
        // Шапка счёта при этом авторитетна — суммы перенесены из согласования как есть;
        // если каталог изменился после согласования, строки могут разойтись с CostPrice.
        var quote = await quotes.GenerateAsync(
            approval.SpecificationId, Enum.Parse<QuoteStrategy>(approval.Strategy), ct: ct);
        foreach (var l in quote.Lines.Where(x => x.Matched))
        {
            var unitPrice = Math.Round(l.UnitPriceDiscounted * (1m + approval.MarkupPercent / 100m), 2);
            inv.Lines.Add(new InvoiceLine
            {
                ProductId = l.ProductId,
                Sku = l.Sku,
                Name = l.Name,
                Quantity = l.Quantity,
                VendorName = l.VendorName,
                Manufacturer = l.Manufacturer,
                UnitCost = l.UnitPriceDiscounted,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * l.Quantity
            });
        }

        db.Set<Invoice>().Add(inv);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(inv.Id, ct), null, false);
    }

    public async Task<InvoiceStatusResult> SetStatusAsync(
        Guid id, SetInvoiceStatusRequest req, CancellationToken ct = default)
    {
        var inv = await db.Set<Invoice>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (inv is null) return new InvoiceStatusResult(false, false, null);
        if (inv.Status == req.Status) return new InvoiceStatusResult(true, true, null);
        if (!IsValidTransition(inv.Status, req.Status))
            return new InvoiceStatusResult(true, false, $"Недопустимый переход статуса: «{inv.Status}» → «{req.Status}»");

        inv.Status = req.Status;
        inv.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new InvoiceStatusResult(true, true, null);
    }

    public async Task<InvoiceAttachmentDto?> UploadAttachmentAsync(
        Guid invoiceId, string fileName, string contentType, byte[] content, string? uploadedBy, CancellationToken ct = default)
    {
        if (!await db.Set<Invoice>().AnyAsync(i => i.Id == invoiceId, ct)) return null;
        var att = new InvoiceAttachment
        {
            InvoiceId = invoiceId,
            FileName = fileName,
            ContentType = contentType,
            FileContent = content,
            UploadedBy = uploadedBy
        };
        db.Set<InvoiceAttachment>().Add(att);
        await db.SaveChangesAsync(ct);
        return new InvoiceAttachmentDto(att.Id, att.InvoiceId, att.FileName, att.ContentType, att.UploadedBy, att.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<InvoiceAttachmentDto>> ListAttachmentsAsync(Guid invoiceId, CancellationToken ct = default) =>
        await db.Set<InvoiceAttachment>().AsNoTracking()
            .Where(a => a.InvoiceId == invoiceId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new InvoiceAttachmentDto(a.Id, a.InvoiceId, a.FileName, a.ContentType, a.UploadedBy, a.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetAttachmentFileAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var a = await db.Set<InvoiceAttachment>().AsNoTracking()
            .Where(x => x.Id == attachmentId)
            .Select(x => new { x.FileContent, x.ContentType, x.FileName })
            .FirstOrDefaultAsync(ct);
        return a is null ? null : (a.FileContent, a.ContentType, a.FileName);
    }

    /// <summary>Следующий последовательный номер счёта: NOC-{год}-NNN.</summary>
    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var prefix = $"NOC-{DateTime.UtcNow.Year}-";
        var existing = await db.Set<Invoice>().AsNoTracking()
            .Where(i => i.Number.StartsWith(prefix))
            .Select(i => i.Number).ToListAsync(ct);
        var max = existing
            .Select(n => n.Length > prefix.Length && int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D3}";
    }

    /// <summary>
    /// Допустимый переход: только на следующую по порядку стадию ЖЦ (без пропуска стадий —
    /// как в OrderService.Transitions), либо «Отменён» из любого не финального состояния.
    /// Финальные: «ОтраженоВ1С» (завершён) и «Отменён».
    /// </summary>
    private static bool IsValidTransition(InvoiceStatus current, InvoiceStatus target)
    {
        if (target == InvoiceStatus.Отменён)
            return current != InvoiceStatus.Отменён && current != InvoiceStatus.ОтраженоВ1С;

        var ci = Array.IndexOf(LinearFlow, current);
        var ti = Array.IndexOf(LinearFlow, target);
        return ci >= 0 && ti == ci + 1;   // строго следующая стадия
    }

    private static InvoiceDetailDto MapDetail(Invoice i) => new(
        i.Id, i.Number, i.ApprovalId, i.Customer, i.Contract,
        i.CostPrice, i.MarkupPercent, i.SellPrice, i.Status,
        i.DueDateUtc, i.CreatedBy, i.CreatedAtUtc,
        i.Lines.OrderBy(l => l.Name).Select(l => new InvoiceLineDto(
            l.Id, l.ProductId, l.Sku, l.Name, l.Quantity,
            l.VendorName, l.Manufacturer, l.UnitCost, l.UnitPrice, l.LineTotal)).ToList());
}
