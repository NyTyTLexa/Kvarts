using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Pricing;

public record PriceHistoryDto(Guid VendorId, decimal Price, int LeadTimeDays, DateTime RecordedAtUtc);

public interface IPriceHistoryService
{
    Task<IReadOnlyList<PriceHistoryDto>> GetForProductAsync(Guid productId, CancellationToken ct = default);
}

/// <summary>Чтение истории цен товара.</summary>
public class PriceHistoryService(CatalogDbContext db) : IPriceHistoryService
{
    public async Task<IReadOnlyList<PriceHistoryDto>> GetForProductAsync(Guid productId, CancellationToken ct = default) =>
        await db.PriceHistory.AsNoTracking()
            .Where(h => h.ProductId == productId)
            .OrderByDescending(h => h.RecordedAtUtc)
            .Select(h => new PriceHistoryDto(h.VendorId, h.Price, h.LeadTimeDays, h.RecordedAtUtc))
            .ToListAsync(ct);
}

public static class PriceHistoryExtensions
{
    /// <summary>
    /// Зафиксировать точку истории цены. Добавляет запись в текущий контекст —
    /// сохраняется в той же транзакции, что и изменение оффера (как и Outbox-событие).
    /// </summary>
    public static void RecordPriceChange(this CatalogDbContext db, Guid productId, Guid vendorId, decimal price, int leadTimeDays) =>
        db.PriceHistory.Add(new PriceHistoryEntry
        {
            ProductId = productId,
            VendorId = vendorId,
            Price = price,
            LeadTimeDays = leadTimeDays
        });
}
