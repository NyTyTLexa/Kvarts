using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Services.Catalog.Common;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Catalog;

public record ManufacturerDto(Guid Id, string Name, string? Country, bool IsActive, int ProductsCount);
public record CreateManufacturerRequest(string Name, string? Country);
public record UpdateManufacturerRequest(string Name, string? Country, bool IsActive);

public interface IManufacturerService
{
    Task<PagedResult<ManufacturerDto>> ListAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<ManufacturerDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(ManufacturerDto? Dto, string? Error)> CreateAsync(CreateManufacturerRequest req, CancellationToken ct = default);
    Task<bool> UpdateAsync(Guid id, UpdateManufacturerRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Ведение справочника производителей (ТЗ п.5.2). ProductsCount считается по совпадению имени
/// с Product.Manufacturer (денормализованное строковое поле) — справочник не переносит каталог
/// на FK, только даёт единое место ведения списка брендов.
/// </summary>
public class ManufacturerService(CatalogDbContext db) : IManufacturerService
{
    public async Task<PagedResult<ManufacturerDto>> ListAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.Set<Manufacturer>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(m => EF.Functions.ILike(m.Name, $"%{search}%"));

        var total = await q.CountAsync(ct);
        var page_ = await q.OrderBy(m => m.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = await AttachCountsAsync(page_, ct);

        return new PagedResult<ManufacturerDto>(items, total, page, pageSize);
    }

    public async Task<ManufacturerDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Set<Manufacturer>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return null;
        return (await AttachCountsAsync([m], ct))[0];
    }

    public async Task<(ManufacturerDto? Dto, string? Error)> CreateAsync(CreateManufacturerRequest req, CancellationToken ct = default)
    {
        var name = req.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, "Название обязательно");
        if (await db.Set<Manufacturer>().AnyAsync(m => m.Name == name, ct))
            return (null, "Производитель с таким названием уже есть");

        var m = new Manufacturer { Name = name, Country = req.Country?.Trim() };
        db.Add(m);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(m.Id, ct), null);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateManufacturerRequest req, CancellationToken ct = default)
    {
        var m = await db.Set<Manufacturer>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        m.Name = req.Name.Trim();
        m.Country = req.Country?.Trim();
        m.IsActive = req.IsActive;
        m.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Set<Manufacturer>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        db.Remove(m);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<List<ManufacturerDto>> AttachCountsAsync(List<Manufacturer> list, CancellationToken ct)
    {
        var names = list.Select(m => m.Name).ToList();
        var counts = await db.Products.AsNoTracking()
            .Where(p => p.Manufacturer != null && names.Contains(p.Manufacturer))
            .GroupBy(p => p.Manufacturer!)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count, ct);

        return list.Select(m => new ManufacturerDto(
            m.Id, m.Name, m.Country, m.IsActive, counts.GetValueOrDefault(m.Name, 0))).ToList();
    }
}
