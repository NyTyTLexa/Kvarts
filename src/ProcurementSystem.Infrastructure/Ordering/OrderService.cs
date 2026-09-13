using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Infrastructure.Ordering;

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

/// <summary>Заказы: формирование из КП и продвижение по жизненному циклу с проверкой переходов.</summary>
public class OrderService(AppDbContext db, IQuoteGenerator quotes) : IOrderService
{
    // Допустимые переходы статусов.
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Draft]     = [OrderStatus.Placed, OrderStatus.Cancelled],
            [OrderStatus.Placed]    = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped]   = [OrderStatus.Completed],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    public async Task<OrderDetailDto?> CreateFromQuoteAsync(Guid specId, QuoteStrategy strategy,
        double wPrice, double wLead, bool onlyInStock, string? createdBy, CancellationToken ct = default)
    {
        if (!await db.Specifications.AnyAsync(s => s.Id == specId, ct)) return null;
        var quote = await quotes.GenerateAsync(specId, strategy, wPrice, wLead, onlyInStock, ct);
        var matched = quote.Lines.Where(l => l.Matched).ToList();
        if (matched.Count == 0) return null;

        var customer = await db.Specifications.AsNoTracking()
            .Where(s => s.Id == specId).Select(s => s.Customer).FirstOrDefaultAsync(ct);

        var order = new Order
        {
            Number = $"ORD-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            SpecificationId = specId,
            Title = quote.SpecificationTitle,
            Customer = customer,
            Strategy = strategy.ToString(),
            Status = OrderStatus.Draft,
            TotalCost = quote.TotalCost,
            MaxLeadTimeDays = quote.MaxLeadTimeDays,
            CreatedBy = createdBy
        };
        foreach (var l in matched)
            order.Lines.Add(new OrderLine
            {
                ProductId = l.ProductId, Sku = l.Sku, Name = l.Name, Quantity = l.Quantity,
                VendorId = l.VendorId, VendorName = l.VendorName,
                UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, LeadTimeDays = l.LeadTimeDays
            });

        db.Orders.Add(order);
        // Резерв остатка (ТЗ UC-04: «подтверждение поставщика и резерва») — оформление
        // заказа сразу уменьшает витринный остаток выбранных офферов.
        await AdjustStockAsync(matched.Select(l => (l.ProductId, l.VendorId, l.Quantity)), -1, ct);
        await db.SaveChangesAsync(ct);
        return ToDetail(order);
    }

    /// <summary>Резервирует (sign=-1) или возвращает (sign=+1) остаток по выбранным офферам.
    /// Не уходит ниже нуля; строки без сопоставленного товара/поставщика пропускаются.</summary>
    private async Task AdjustStockAsync(IEnumerable<(Guid? ProductId, Guid? VendorId, int Quantity)> lines, int sign, CancellationToken ct)
    {
        foreach (var l in lines)
        {
            if (l.ProductId is not { } pid || l.VendorId is not { } vid) continue;
            var offer = await db.PriceListItems.FirstOrDefaultAsync(o => o.ProductId == pid && o.VendorId == vid, ct);
            if (offer is null) continue;
            offer.StockQuantity = Math.Max(0, offer.StockQuantity + sign * l.Quantity);
        }
    }

    public async Task<IReadOnlyList<OrderDto>> ListAsync(CancellationToken ct = default) =>
        await db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrderDto(o.Id, o.Number, o.SpecificationId, o.Title, o.Customer, o.Strategy, o.Status,
                o.TotalCost, o.MaxLeadTimeDays, o.Lines.Count, o.CreatedBy, o.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<OrderDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? null : ToDetail(o);
    }

    public async Task<StatusChangeResult> ChangeStatusAsync(Guid id, OrderStatus target, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return new StatusChangeResult(false, false, null);
        if (order.Status == target) return new StatusChangeResult(true, true, null);
        if (!Transitions[order.Status].Contains(target))
            return new StatusChangeResult(true, false, $"Недопустимый переход {order.Status} → {target}");

        // Отмена заказа — возвращаем зарезервированный остаток обратно на витрину.
        if (target == OrderStatus.Cancelled)
            await AdjustStockAsync(order.Lines.Select(l => (l.ProductId, l.VendorId, l.Quantity)), +1, ct);

        order.Status = target;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new StatusChangeResult(true, true, null);
    }

    private static OrderDetailDto ToDetail(Order o) => new(
        o.Id, o.Number, o.Title, o.Customer, o.Strategy, o.Status, o.TotalCost, o.MaxLeadTimeDays,
        o.CreatedBy, o.CreatedAtUtc,
        o.Lines.OrderBy(l => l.Name).Select(l => new OrderLineDto(
            l.Id, l.ProductId, l.Sku, l.Name, l.Quantity, l.VendorId, l.VendorName,
            l.UnitPrice, l.LineTotal, l.LeadTimeDays)).ToList(),
        o.SpecificationId);
}
