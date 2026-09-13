using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Infrastructure.Common;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Catalog;

public interface IVendorService
{
    Task<PagedResult<VendorDto>> ListAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<VendorDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<VendorDto> CreateAsync(CreateVendorRequest req, CancellationToken ct = default);
    Task<bool> UpdateAsync(Guid id, UpdateVendorRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Ведение справочника поставщиков.</summary>
public class VendorService(AppDbContext db) : IVendorService
{
    public async Task<PagedResult<VendorDto>> ListAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.Vendors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(v => EF.Functions.ILike(v.Name, $"%{search}%"));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new VendorDto(v.Id, v.Name, v.Inn, v.DefaultLeadTimeDays))
            .ToListAsync(ct);

        return new PagedResult<VendorDto>(items, total, page, pageSize);
    }

    public async Task<VendorDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var v = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return v is null ? null : new VendorDto(v.Id, v.Name, v.Inn, v.DefaultLeadTimeDays);
    }

    public async Task<VendorDto> CreateAsync(CreateVendorRequest req, CancellationToken ct = default)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new ArgumentException("Название поставщика обязательно.");
        var v = new Vendor { Name = name, Inn = req.Inn, DefaultLeadTimeDays = Math.Max(1, req.DefaultLeadTimeDays) };
        db.Vendors.Add(v);
        await db.SaveChangesAsync(ct);
        return new VendorDto(v.Id, v.Name, v.Inn, v.DefaultLeadTimeDays);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateVendorRequest req, CancellationToken ct = default)
    {
        var v = await db.Vendors.FindAsync([id], ct);
        if (v is null) return false;
        v.Name = req.Name;
        v.Inn = req.Inn;
        v.DefaultLeadTimeDays = req.DefaultLeadTimeDays;
        v.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var v = await db.Vendors.FindAsync([id], ct);
        if (v is null) return false;
        db.Vendors.Remove(v);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
