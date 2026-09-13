using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>Чтение счетов NOC и их документов (CQRS: query-сторона, только AsNoTracking-проекции).</summary>
public interface IInvoiceQueryService
{
    Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct = default);
    Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceAttachmentDto>> ListAttachmentsAsync(Guid invoiceId, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, string FileName)?> GetAttachmentFileAsync(Guid attachmentId, CancellationToken ct = default);
}

public class InvoiceQueryService(AppDbContext db) : IInvoiceQueryService
{
    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct = default) =>
        // Inline-проекция: Lines.Count транслируется в подзапрос COUNT — как в OrderQueryService.
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
        return inv?.ToDetailDto();
    }

    public async Task<IReadOnlyList<InvoiceAttachmentDto>> ListAttachmentsAsync(Guid invoiceId, CancellationToken ct = default) =>
        await db.Set<InvoiceAttachment>().AsNoTracking()
            .Where(a => a.InvoiceId == invoiceId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetAttachmentFileAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var a = await db.Set<InvoiceAttachment>().AsNoTracking()
            .Where(x => x.Id == attachmentId)
            .Select(x => new { x.FileContent, x.ContentType, x.FileName })
            .FirstOrDefaultAsync(ct);
        return a is null ? null : (a.FileContent, a.ContentType, a.FileName);
    }
}
