using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Infrastructure.Outbox;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;

namespace ProcurementSystem.Infrastructure.Catalog;

public interface IOfferService
{
    Task<IReadOnlyList<OfferDto>> ByProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Создать оффер. Возвращает null, если товар или поставщик не найдены.</summary>
    Task<OfferDto?> CreateAsync(CreateOfferRequest req, CancellationToken ct = default);
}

/// <summary>Предложения поставщиков (офферы) по товарам. Создание оффера меняет индексную карточку товара.</summary>
public class OfferService(AppDbContext db) : IOfferService
{
    public async Task<IReadOnlyList<OfferDto>> ByProductAsync(Guid productId, CancellationToken ct = default) =>
        await db.PriceListItems.AsNoTracking()
            .Where(o => o.ProductId == productId)
            .OrderBy(o => o.Price)
            .Select(o => new OfferDto(o.Id, o.ProductId, o.VendorId, o.Vendor.Name,
                o.Price, o.Currency, o.LeadTimeDays, o.StockQuantity, o.SourceUrl))
            .ToListAsync(ct);

    public async Task<OfferDto?> CreateAsync(CreateOfferRequest req, CancellationToken ct = default)
    {
        var product = await db.Products.FindAsync([req.ProductId], ct);
        var vendor = await db.Vendors.FindAsync([req.VendorId], ct);
        if (product is null || vendor is null) return null;

        var o = new PriceListItem
        {
            ProductId = req.ProductId,
            VendorId = req.VendorId,
            Price = req.Price,
            LeadTimeDays = req.LeadTimeDays,
            StockQuantity = req.StockQuantity
        };
        db.PriceListItems.Add(o);
        db.EnqueueEvent(new ProductUpserted(o.ProductId, DateTime.UtcNow));
        db.RecordPriceChange(o.ProductId, o.VendorId, o.Price, o.LeadTimeDays);
        await db.SaveChangesAsync(ct);

        return new OfferDto(o.Id, o.ProductId, o.VendorId, vendor.Name,
            o.Price, o.Currency, o.LeadTimeDays, o.StockQuantity, o.SourceUrl);
    }
}
