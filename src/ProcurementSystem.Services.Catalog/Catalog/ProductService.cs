using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Services.Catalog.Common;
using ProcurementSystem.Services.Catalog.Outbox;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Catalog;

public interface IProductService
{
    Task<PagedResult<ProductDto>> ListAsync(int page, int pageSize, string? search, string? category = null, CancellationToken ct = default);
    Task<ProductDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest req, CancellationToken ct = default);
    Task<bool> UpdateAsync(Guid id, UpdateProductRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Список категорий (иерархические пути через « / ») с количеством позиций — для фильтра каталога.</summary>
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Аналоги товара (ТЗ п.5.3): позиции той же (нижней) категории, кроме самого товара.</summary>
    Task<IReadOnlyList<ProductDto>?> GetAnalogsAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Ведение номенклатуры. Любое изменение порождает событие ProductUpserted (Outbox → индекс).</summary>
public class ProductService(CatalogDbContext db) : IProductService
{
    public async Task<PagedResult<ProductDto>> ListAsync(int page, int pageSize, string? search, string? category = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => EF.Functions.ILike(p.Name, $"%{search}%")
                          || EF.Functions.ILike(p.Sku, $"%{search}%"));
        // Фильтр по категории — префиксный: выбор верхнего уровня иерархии показывает всё поддерево.
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(p => p.Category != null && p.Category.StartsWith(category));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Manufacturer, p.Category))
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, total, page, pageSize);
    }

    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : new ProductDto(p.Id, p.Sku, p.Name, p.Manufacturer, p.Category);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest req, CancellationToken ct = default)
    {
        var p = new Product { Sku = req.Sku, Name = req.Name, Manufacturer = req.Manufacturer, Category = req.Category };
        db.Products.Add(p);
        db.EnqueueEvent(new ProductUpserted(p.Id, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
        return new ProductDto(p.Id, p.Sku, p.Name, p.Manufacturer, p.Category);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProductRequest req, CancellationToken ct = default)
    {
        var p = await db.Products.FindAsync([id], ct);
        if (p is null) return false;
        p.Sku = req.Sku;
        p.Name = req.Name;
        p.Manufacturer = req.Manufacturer;
        p.Category = req.Category;
        p.UpdatedAtUtc = DateTime.UtcNow;
        db.EnqueueEvent(new ProductUpserted(p.Id, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Products.FindAsync([id], ct);
        if (p is null) return false;
        db.Products.Remove(p);
        // то же событие: индексатор не найдёт товар в БД и удалит документ из индекса
        db.EnqueueEvent(new ProductUpserted(p.Id, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Category != null)
            .GroupBy(p => p.Category!)
            .OrderBy(g => g.Key)
            .Take(500)
            .Select(g => new CategoryDto(g.Key, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductDto>?> GetAnalogsAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        if (string.IsNullOrWhiteSpace(p.Category)) return [];

        // Аналог = та же (нижняя) категория иерархии. Сначала — другие производители
        // (классический сценарий замены), затем свои, по имени.
        return await db.Products.AsNoTracking()
            .Where(x => x.Category == p.Category && x.Id != id)
            .OrderBy(x => x.Manufacturer == p.Manufacturer)
            .ThenBy(x => x.Name)
            .Take(50)
            .Select(x => new ProductDto(x.Id, x.Sku, x.Name, x.Manufacturer, x.Category))
            .ToListAsync(ct);
    }
}
