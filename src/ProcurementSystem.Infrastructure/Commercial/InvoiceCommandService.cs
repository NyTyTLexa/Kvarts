using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>Команды счетов NOC (CQRS: command-сторона — создание, ЖЦ, документы).</summary>
public interface IInvoiceCommandService
{
    Task<(InvoiceDetailDto? Dto, string? Error, bool Conflict)> CreateAsync(CreateInvoiceRequest req, string? createdBy, CancellationToken ct = default);
    Task<InvoiceStatusResult> SetStatusAsync(Guid id, SetInvoiceStatusRequest req, CancellationToken ct = default);
    Task<InvoiceStatusResult> SetStatusAsync(Guid id, SetInvoiceStatusRequest req, InvoiceStatusActor actor, CancellationToken ct = default);

    // Прикреплённые документы (ТЗ п.6.1) — загрузка (список/скачивание — в IInvoiceQueryService).
    Task<InvoiceAttachmentDto?> UploadAttachmentAsync(Guid invoiceId, string fileName, string contentType, byte[] content, string? uploadedBy, CancellationToken ct = default);
}

/// <summary>
/// Счета NOC (ТЗ п.B7). Создаются только из согласованного Approval; сумма, наценка и
/// контрагент переносятся из согласования, позиции — снимок согласованного варианта КП
/// (тот же вызов IQuoteGenerator, что делал ApprovalCommandService). Правила переходов ЖЦ —
/// поведение сущности <see cref="Invoice"/> (rich domain model), недопустимый переход → 409.
/// </summary>
public class InvoiceCommandService(AppDbContext db, IQuoteGenerator quotes) : IInvoiceCommandService
{
    public async Task<(InvoiceDetailDto? Dto, string? Error, bool Conflict)> CreateAsync(
        CreateInvoiceRequest req, string? createdBy, CancellationToken ct = default)
    {
        // UC-07: повторное создание по тому же согласованию — тот же счёт (идемпотентно).
        var existing = await db.Set<Invoice>().Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.ApprovalId == req.ApprovalId, ct);
        if (existing is not null)
            return (existing.ToDetailDto(), null, false);

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
        // с теми же параметрами по умолчанию, что при создании согласования, поэтому
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
        return (inv.ToDetailDto(), null, false);
    }

    public Task<InvoiceStatusResult> SetStatusAsync(
        Guid id, SetInvoiceStatusRequest req, CancellationToken ct = default)
        => SetStatusAsync(id, req, InvoiceStatusActor.System, ct);

    public async Task<InvoiceStatusResult> SetStatusAsync(
        Guid id, SetInvoiceStatusRequest req, InvoiceStatusActor actor, CancellationToken ct = default)
    {
        var inv = await db.Set<Invoice>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (inv is null) return new InvoiceStatusResult(false, false, null);
        if (inv.Status == req.Status) return new InvoiceStatusResult(true, true, null);
        if (!Invoice.ActorMaySet(req.Status, actor))
        {
            var error = req.Status == InvoiceStatus.Согласован
                ? "Стадию «Согласован» выставляет только коммерческий блок"
                : "Последующие стадии счёта выставляет бухгалтерия";
            return new InvoiceStatusResult(true, false, error, Forbidden: true);
        }
        if (!inv.TryTransitionTo(req.Status))
            return new InvoiceStatusResult(true, false, $"Недопустимый переход статуса: «{inv.Status}» → «{req.Status}»");

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
        return att.ToDto();
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
}
