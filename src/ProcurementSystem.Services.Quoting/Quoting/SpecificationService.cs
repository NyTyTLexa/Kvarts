using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Services.Quoting.Clients;
using ProcurementSystem.Services.Quoting.Persistence;

namespace ProcurementSystem.Services.Quoting.Quoting;

public interface ISpecificationService
{
    Task<IReadOnlyList<SpecificationDto>> ListAsync(CancellationToken ct = default);
    Task<SpecificationDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<SpecificationDto> CreateAsync(CreateSpecificationRequest req, CancellationToken ct = default);

    /// <summary>Добавить позицию. Возвращает null, если спецификация не найдена.</summary>
    Task<SpecificationItemDto?> AddItemAsync(Guid specId, AddSpecificationItemRequest req, CancellationToken ct = default);
    Task<bool> DeleteItemAsync(Guid specId, Guid itemId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Зафиксировать ручной выбор поставщика для позиции (ТЗ п.5.5). false — позиция не найдена.</summary>
    Task<bool> SetItemVendorOverrideAsync(Guid specId, Guid itemId, Guid vendorId, string? updatedBy, CancellationToken ct = default);

    /// <summary>Снять ручную корректировку поставщика для позиции.</summary>
    Task<bool> ClearItemVendorOverrideAsync(Guid specId, Guid itemId, CancellationToken ct = default);
}

/// <summary>Ведение спецификаций (заявок) и их позиций. Импорт из Excel — в <see cref="ISpecificationImporter"/>.</summary>
public class SpecificationService(QuotingDbContext db, ICatalogClient catalog) : ISpecificationService
{
    public async Task<IReadOnlyList<SpecificationDto>> ListAsync(CancellationToken ct = default) =>
        await db.Specifications.AsNoTracking()
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SpecificationDto(s.Id, s.Title, s.Customer,
                s.Items.Count, s.Items.Count(i => i.ProductId != null), s.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<SpecificationDetailDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Specifications.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SpecificationDetailDto(s.Id, s.Title, s.Customer, s.CreatedAtUtc,
                s.Items.OrderBy(i => i.RawName).Select(i => new SpecificationItemDto(
                    i.Id, i.ProductId, i.RawSku, i.RawName, i.Quantity, i.ProductId != null)).ToList()))
            .FirstOrDefaultAsync(ct);

    public async Task<SpecificationDto> CreateAsync(CreateSpecificationRequest req, CancellationToken ct = default)
    {
        var s = new Specification { Title = req.Title, Customer = req.Customer };
        db.Specifications.Add(s);
        await db.SaveChangesAsync(ct);
        return new SpecificationDto(s.Id, s.Title, s.Customer, 0, 0, s.CreatedAtUtc);
    }

    public async Task<SpecificationItemDto?> AddItemAsync(Guid specId, AddSpecificationItemRequest req, CancellationToken ct = default)
    {
        var exists = await db.Specifications.AnyAsync(s => s.Id == specId, ct);
        if (!exists) return null;

        var productId = req.ProductId;
        if (productId is null && !string.IsNullOrWhiteSpace(req.Sku))
            productId = (await catalog.FindBySkuAsync(req.Sku, ct))?.Id;

        var item = new SpecificationItem
        {
            SpecificationId = specId,
            ProductId = productId,
            RawSku = string.IsNullOrWhiteSpace(req.Sku) ? null : req.Sku,
            RawName = string.IsNullOrWhiteSpace(req.Name) ? (req.Sku ?? "—") : req.Name,
            Quantity = req.Quantity <= 0 ? 1 : req.Quantity
        };
        db.SpecificationItems.Add(item);
        await db.SaveChangesAsync(ct);
        return new SpecificationItemDto(item.Id, item.ProductId, item.RawSku, item.RawName, item.Quantity, item.ProductId != null);
    }

    public async Task<bool> DeleteItemAsync(Guid specId, Guid itemId, CancellationToken ct = default)
    {
        var item = await db.SpecificationItems.FirstOrDefaultAsync(i => i.Id == itemId && i.SpecificationId == specId, ct);
        if (item is null) return false;
        db.SpecificationItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var spec = await db.Specifications.FindAsync([id], ct);
        if (spec is null) return false;
        db.Specifications.Remove(spec);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetItemVendorOverrideAsync(Guid specId, Guid itemId, Guid vendorId, string? updatedBy, CancellationToken ct = default)
    {
        var exists = await db.SpecificationItems.AnyAsync(i => i.Id == itemId && i.SpecificationId == specId, ct);
        if (!exists) return false;

        var ov = await db.QuoteOverrides.FirstOrDefaultAsync(o => o.SpecificationItemId == itemId, ct);
        if (ov is null)
            db.QuoteOverrides.Add(new QuoteOverride { SpecificationItemId = itemId, VendorId = vendorId, UpdatedBy = updatedBy });
        else
        {
            ov.VendorId = vendorId;
            ov.UpdatedBy = updatedBy;
            ov.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ClearItemVendorOverrideAsync(Guid specId, Guid itemId, CancellationToken ct = default)
    {
        var ov = await db.QuoteOverrides.FirstOrDefaultAsync(o => o.SpecificationItemId == itemId, ct);
        if (ov is null) return false;
        db.QuoteOverrides.Remove(ov);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
