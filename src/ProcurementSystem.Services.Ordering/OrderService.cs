using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Services.Ordering.Clients;
using ProcurementSystem.Services.Ordering.Persistence;

namespace ProcurementSystem.Services.Ordering;

/// <summary>Заказы: формирование из КП и продвижение по жизненному циклу с проверкой переходов.</summary>
public class OrderService(OrderingDbContext db, IQuoteClient quotes, ICatalogStockClient stock) : IOrderService
{
    public async Task<OrderDetailDto?> CreateFromQuoteAsync(Guid specId, QuoteStrategy strategy,
        double wPrice, double wLead, bool onlyInStock, string? createdBy, CancellationToken ct = default)
    {
        var quote = await quotes.GetQuoteAsync(specId, strategy, wPrice, wLead, onlyInStock, ct);
        if (quote is null) return null;

        var matched = (quote.Lines ?? []).Where(l => l.Matched).ToList();
        if (matched.Count == 0) return null;

        var customer = await quotes.GetCustomerAsync(specId, ct);

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
        await stock.AdjustAsync(matched.Select(l => (l.ProductId, l.VendorId, l.Quantity)), -1, ct);
        await db.SaveChangesAsync(ct);
        return ToDetail(order);
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

        var from = order.Status;
        if (!order.TryTransitionTo(target))
            return new StatusChangeResult(true, false, $"Недопустимый переход {from} → {target}");

        if (target == OrderStatus.Cancelled && from != OrderStatus.Cancelled)
            await stock.AdjustAsync(order.Lines.Select(l => (l.ProductId, l.VendorId, l.Quantity)), +1, ct);

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
