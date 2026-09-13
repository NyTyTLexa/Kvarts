using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Catalog;

public record StockAdjustItem(Guid ProductId, Guid VendorId, int Quantity, int Sign);
public record StockAdjustRequest(IReadOnlyList<StockAdjustItem>? Items);

public interface IStockAdjustService
{
    /// <summary>
    /// Резерв (sign=-1) или возврат (sign=+1) остатка по паре товар+поставщик.
    /// Неизвестные офферы пропускаются; ниже нуля не уходим. Один SaveChanges на пакет.
    /// </summary>
    Task AdjustAsync(IReadOnlyList<StockAdjustItem>? items, CancellationToken ct = default);
}

/// <summary>Пакетное изменение витринного остатка. Семантика как у монолитного OrderService.AdjustStockAsync.</summary>
public class StockAdjustService(CatalogDbContext db) : IStockAdjustService
{
    public async Task AdjustAsync(IReadOnlyList<StockAdjustItem>? items, CancellationToken ct = default)
    {
        if (items is null || items.Count == 0) return;

        foreach (var item in items)
        {
            if (item.Quantity == 0) continue;
            var offer = await db.PriceListItems
                .FirstOrDefaultAsync(o => o.ProductId == item.ProductId && o.VendorId == item.VendorId, ct);
            if (offer is null) continue;
            // Sign — только направление: sign=5 не должен умножать количество.
            offer.StockQuantity = Math.Max(0, offer.StockQuantity + Math.Sign(item.Sign) * Math.Abs(item.Quantity));
            offer.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
