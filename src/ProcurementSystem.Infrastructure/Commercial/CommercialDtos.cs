using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Infrastructure.Commercial;

// ── Согласование КП (ТЗ п.B6) ──────────────────────────────────────

public record ApprovalDto(
    Guid Id, Guid SpecificationId, string Strategy, string Title, string? Customer,
    decimal CostPrice, decimal MarkupPercent, decimal SellPrice, decimal MarginPercent,
    ApprovalStatus Status, string? CreatedBy, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? Comment);

public record CreateApprovalRequest(Guid SpecificationId, QuoteStrategy Strategy, decimal MarkupPercent);
public record SetMarginRequest(decimal MarkupPercent);
public record DecisionRequest(bool Approved, string? Comment);

// ── Счёт NOC (ТЗ п.B7) ─────────────────────────────────────────────

public record InvoiceDto(
    Guid Id, string Number, Guid ApprovalId, string? Customer, string? Contract,
    decimal CostPrice, decimal MarkupPercent, decimal SellPrice, InvoiceStatus Status,
    DateTime? DueDateUtc, string? CreatedBy, DateTime CreatedAtUtc, int LinesCount);

/// <summary>Строка счёта — снимок позиции согласованного варианта КП (ТЗ п.6.1).</summary>
public record InvoiceLineDto(
    Guid Id, Guid? ProductId, string? Sku, string Name, int Quantity,
    string? VendorName, string? Manufacturer,
    decimal UnitCost, decimal UnitPrice, decimal LineTotal);

/// <summary>Счёт с позициями — отдаётся детальным GET и создающим POST.</summary>
public record InvoiceDetailDto(
    Guid Id, string Number, Guid ApprovalId, string? Customer, string? Contract,
    decimal CostPrice, decimal MarkupPercent, decimal SellPrice, InvoiceStatus Status,
    DateTime? DueDateUtc, string? CreatedBy, DateTime CreatedAtUtc,
    IReadOnlyList<InvoiceLineDto> Lines);

public record CreateInvoiceRequest(Guid ApprovalId, string? Contract, DateTime? DueDateUtc);
public record SetInvoiceStatusRequest(InvoiceStatus Status);

/// <summary>Результат смены статуса счёта — по образцу OrderService.StatusChangeResult.
/// Forbidden — роль не может выставить целевую стадию (ТЗ 10.11); дефолт false, чтобы старые вызовы компилировались.</summary>
public record InvoiceStatusResult(bool Found, bool Ok, string? Error, bool Forbidden = false);

// ── Прикреплённые документы счёта (ТЗ п.6.1) ────────────────────────

public record InvoiceAttachmentDto(
    Guid Id, Guid InvoiceId, string FileName, string ContentType,
    string? UploadedBy, DateTime UploadedAtUtc);
