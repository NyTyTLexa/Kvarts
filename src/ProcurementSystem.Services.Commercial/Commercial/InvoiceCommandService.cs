using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Services.Commercial.Neighbors;
using ProcurementSystem.Services.Commercial.Persistence;

namespace ProcurementSystem.Services.Commercial.Commercial;

/// <summary>Команды счетов NOC (CQRS: command-сторона — создание, ЖЦ, документы).</summary>
public interface IInvoiceCommandService
{
    Task<(InvoiceDetailDto? Dto, string? Error, bool Conflict)> CreateAsync(CreateInvoiceRequest req, string? createdBy, CancellationToken ct = default);
    /// <summary>Смена стадии системным актором (склад / совместимость со старыми вызовами).</summary>
    Task<InvoiceStatusResult> SetStatusAsync(Guid id, SetInvoiceStatusRequest req, CancellationToken ct = default);
    Task<InvoiceStatusResult> SetStatusAsync(Guid id, SetInvoiceStatusRequest req, InvoiceStatusActor actor, CancellationToken ct = default);
    Task<InvoiceAttachmentDto?> UploadAttachmentAsync(Guid invoiceId, string fileName, string contentType, byte[] content, string? uploadedBy, CancellationToken ct = default);
}

/// <summary>
/// Счета NOC (ТЗ п.B7). Создаются только из согласованного Approval; сумма, наценка и
/// контрагент переносятся из согласования, позиции — снимок согласованного варианта КП
/// (тот же вызов quoting, что делал ApprovalCommandService). Правила переходов ЖЦ —
/// поведение сущности <see cref="Invoice"/>, недопустимый переход → 409; роль —
/// <see cref="Invoice.ActorMaySet"/>, отказ → 403.
/// </summary>
public class InvoiceCommandService(CommercialDbContext db, IQuotingClient quoting) : IInvoiceCommandService
{
    public async Task<(InvoiceDetailDto? Dto, string? Error, bool Conflict)> CreateAsync(
        CreateInvoiceRequest req, string? createdBy, CancellationToken ct = default)
    {
        // UC-07: повтор по тому же ApprovalId — вернуть существующий, не второй счёт.
        var existing = await db.Invoices.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.ApprovalId == req.ApprovalId, ct);
        if (existing is not null) return (existing.ToDetailDto(), null, false);

        var approval = await db.Approvals.AsNoTracking()
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

        if (!Enum.TryParse<QuoteStrategy>(approval.Strategy, ignoreCase: true, out var strategy))
            return (null, $"Неизвестная стратегия КП в согласовании: «{approval.Strategy}»", false);

        NeighborQuote? quote;
        try
        {
            quote = await quoting.GenerateQuoteAsync(approval.SpecificationId, strategy, ct);
        }
        catch (HttpRequestException ex)
        {
            return (null, ex.Message, false);
        }
        if (quote is null)
            return (null, "Не удалось получить снимок КП по спецификации согласования", false);

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

        db.Invoices.Add(inv);
        await db.SaveChangesAsync(ct);
        return (inv.ToDetailDto(), null, false);
    }

    public Task<InvoiceStatusResult> SetStatusAsync(
        Guid id, SetInvoiceStatusRequest req, CancellationToken ct = default)
        => SetStatusAsync(id, req, InvoiceStatusActor.System, ct);

    public async Task<InvoiceStatusResult> SetStatusAsync(
        Guid id, SetInvoiceStatusRequest req, InvoiceStatusActor actor, CancellationToken ct = default)
    {
        var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (inv is null) return new InvoiceStatusResult(false, false, null);
        // Повтор текущей стадии идемпотентен — роль не проверяем.
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
        if (!await db.Invoices.AnyAsync(i => i.Id == invoiceId, ct)) return null;
        var att = new InvoiceAttachment
        {
            InvoiceId = invoiceId,
            FileName = fileName,
            ContentType = contentType,
            FileContent = content,
            UploadedBy = uploadedBy
        };
        db.InvoiceAttachments.Add(att);
        await db.SaveChangesAsync(ct);
        return att.ToDto();
    }

    /// <summary>Следующий последовательный номер счёта: NOC-{год}-NNN.</summary>
    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var prefix = $"NOC-{DateTime.UtcNow.Year}-";
        var existing = await db.Invoices.AsNoTracking()
            .Where(i => i.Number.StartsWith(prefix))
            .Select(i => i.Number).ToListAsync(ct);
        var max = existing
            .Select(n => n.Length > prefix.Length && int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D3}";
    }
}
