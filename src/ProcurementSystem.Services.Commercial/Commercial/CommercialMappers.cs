using ProcurementSystem.Domain.Commercial;

namespace ProcurementSystem.Services.Commercial.Commercial;

/// <summary>
/// Маппинг сущностей коммерческого контура в DTO — общий для Query- и Command-сервисов
/// (CQRS: команды возвращают представление изменённого агрегата тем же маппером, что и запросы).
/// </summary>
public static class CommercialMappers
{
    public static ApprovalDto ToDto(this Approval a) => new(
        a.Id, a.SpecificationId, a.Strategy, a.Title, a.Customer,
        a.CostPrice, a.MarkupPercent, a.SellPrice, a.MarginPercent,
        a.Status, a.CreatedBy, a.CreatedAtUtc, a.DecidedAtUtc, a.Comment);

    public static InvoiceDetailDto ToDetailDto(this Invoice i) => new(
        i.Id, i.Number, i.ApprovalId, i.Customer, i.Contract,
        i.CostPrice, i.MarkupPercent, i.SellPrice, i.Status,
        i.DueDateUtc, i.CreatedBy, i.CreatedAtUtc,
        i.Lines.OrderBy(l => l.Name).Select(l => new InvoiceLineDto(
            l.Id, l.ProductId, l.Sku, l.Name, l.Quantity,
            l.VendorName, l.Manufacturer, l.UnitCost, l.UnitPrice, l.LineTotal)).ToList());

    public static InvoiceAttachmentDto ToDto(this InvoiceAttachment a) =>
        new(a.Id, a.InvoiceId, a.FileName, a.ContentType, a.UploadedBy, a.CreatedAtUtc);
}
