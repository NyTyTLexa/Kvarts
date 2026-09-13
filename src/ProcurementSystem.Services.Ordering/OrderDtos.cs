using ProcurementSystem.Domain.Ordering;

namespace ProcurementSystem.Services.Ordering;

public record OrderDto(
    Guid Id, string Number, Guid? SpecificationId, string Title, string? Customer, string Strategy, OrderStatus Status,
    decimal TotalCost, int MaxLeadTimeDays, int LinesCount, string? CreatedBy, DateTime CreatedAtUtc);

public record OrderLineDto(
    Guid Id, Guid? ProductId, string? Sku, string Name, int Quantity,
    Guid? VendorId, string? VendorName, decimal UnitPrice, decimal LineTotal, int LeadTimeDays);

public record OrderDetailDto(
    Guid Id, string Number, string Title, string? Customer, string Strategy, OrderStatus Status,
    decimal TotalCost, int MaxLeadTimeDays, string? CreatedBy, DateTime CreatedAtUtc,
    IReadOnlyList<OrderLineDto> Lines, Guid? SpecificationId = null);

public record ChangeOrderStatusRequest(OrderStatus Status);
public record StatusChangeResult(bool Found, bool Ok, string? Error);

public interface IOrderService
{
    /// <summary>Оформить заказ из КП (снимок выбранного варианта). null — если в КП нет сопоставленных позиций.</summary>
    Task<OrderDetailDto?> CreateFromQuoteAsync(Guid specId, QuoteStrategy strategy,
        double wPrice, double wLead, bool onlyInStock, string? createdBy, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDto>> ListAsync(CancellationToken ct = default);
    Task<OrderDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<StatusChangeResult> ChangeStatusAsync(Guid id, OrderStatus target, CancellationToken ct = default);
}
