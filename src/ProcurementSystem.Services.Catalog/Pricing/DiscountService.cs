using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Pricing;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Pricing;

/// <summary>Цель скидки: по поставщику или по производителю (ТЗ п.5.4 — «по вендору и/или производителю»).</summary>
public enum DiscountTarget { Vendor, Manufacturer }

public record DiscountDto(
    Guid Id, DiscountTarget Target, Guid? VendorId, string? VendorName, string? VendorInn,
    string? Manufacturer, decimal Percent, DateTime? ValidFromUtc, DateTime? ValidToUtc,
    string? CreatedBy, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, bool IsActive);

public record CreateDiscountRequest(Guid? VendorId, string? Manufacturer, decimal Percent, DateTime? ValidFromUtc, DateTime? ValidToUtc);
public record UpdateDiscountRequest(decimal Percent, DateTime? ValidFromUtc, DateTime? ValidToUtc);

/// <summary>Лёгкая проекция активной скидки для генератора КП: кого касается и сколько процентов.</summary>
public record ApplicableDiscount(Guid? VendorId, string? Manufacturer, decimal Percent);

public interface IDiscountService
{
    Task<IReadOnlyList<DiscountDto>> ListAsync(DiscountTarget? target, string? search, CancellationToken ct = default);
    Task<DiscountDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(DiscountDto? Dto, string? ValidationError)> CreateAsync(CreateDiscountRequest req, string? createdBy, CancellationToken ct = default);
    Task<(bool Found, string? ValidationError)> UpdateAsync(Guid id, UpdateDiscountRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicableDiscount>> GetActiveForAsync(IReadOnlyCollection<Guid> vendorIds, IReadOnlyCollection<string> manufacturers, CancellationToken ct = default);
}

/// <summary>
/// Ведение скидок по вендорам/производителям (ТЗ UC-03, п.5.4). Скидка имеет ровно одну цель,
/// процент 0..100, срок действия (с/по, nullable = бессрочно). На одну цель — не более одной записи
/// (новый период = редактирование существующей). HTTP-аудит живёт в монолите / будущем
/// audit-сервисе — этот хост его не пишет.
/// </summary>
public class DiscountService(CatalogDbContext db) : IDiscountService
{
    public async Task<IReadOnlyList<DiscountDto>> ListAsync(DiscountTarget? target, string? search, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var q = db.Discounts.AsNoTracking().Include(d => d.Vendor).AsQueryable();

        if (target == DiscountTarget.Vendor) q = q.Where(d => d.VendorId != null);
        else if (target == DiscountTarget.Manufacturer) q = q.Where(d => d.Manufacturer != null);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(d => (d.Vendor != null && EF.Functions.ILike(d.Vendor.Name, $"%{search}%"))
                          || (d.Manufacturer != null && EF.Functions.ILike(d.Manufacturer, $"%{search}%")));

        return await q.OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DiscountDto(
                d.Id,
                d.VendorId != null ? DiscountTarget.Vendor : DiscountTarget.Manufacturer,
                d.VendorId, d.Vendor != null ? d.Vendor.Name : null, d.Vendor != null ? d.Vendor.Inn : null,
                d.Manufacturer, d.Percent, d.ValidFromUtc, d.ValidToUtc, d.CreatedBy, d.CreatedAtUtc, d.UpdatedAtUtc,
                (d.ValidFromUtc == null || d.ValidFromUtc <= now) && (d.ValidToUtc == null || d.ValidToUtc >= now)))
            .ToListAsync(ct);
    }

    public async Task<DiscountDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var d = await db.Discounts.AsNoTracking().Include(x => x.Vendor).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        return new DiscountDto(
            d.Id, d.VendorId != null ? DiscountTarget.Vendor : DiscountTarget.Manufacturer,
            d.VendorId, d.Vendor?.Name, d.Vendor?.Inn, d.Manufacturer,
            d.Percent, d.ValidFromUtc, d.ValidToUtc, d.CreatedBy, d.CreatedAtUtc, d.UpdatedAtUtc,
            (d.ValidFromUtc == null || d.ValidFromUtc <= now) && (d.ValidToUtc == null || d.ValidToUtc >= now));
    }

    public async Task<(DiscountDto? Dto, string? ValidationError)> CreateAsync(
        CreateDiscountRequest req, string? createdBy, CancellationToken ct = default)
    {
        var (ok, err, vendorId, manufacturer) = ValidateTarget(req.VendorId, req.Manufacturer);
        if (!ok) return (null, err);
        if (req.Percent is < 0 or > 100) return (null, "Процент скидки должен быть в диапазоне 0..100");
        if (req.ValidFromUtc is not null && req.ValidToUtc is not null && req.ValidToUtc < req.ValidFromUtc)
            return (null, "Дата окончания раньше даты начала");

        if (vendorId is not null && await db.Discounts.AnyAsync(d => d.VendorId == vendorId, ct))
            return (null, "У этого поставщика уже есть скидка — отредактируйте существующую");
        if (manufacturer is not null && await db.Discounts.AnyAsync(d => d.Manufacturer == manufacturer, ct))
            return (null, "У этого производителя уже есть скидка — отредактируйте существующую");
        if (vendorId is not null && !await db.Vendors.AnyAsync(v => v.Id == vendorId, ct))
            return (null, "Поставщик не найден");

        var d = new Discount
        {
            VendorId = vendorId, Manufacturer = manufacturer, Percent = req.Percent,
            ValidFromUtc = req.ValidFromUtc, ValidToUtc = req.ValidToUtc, CreatedBy = createdBy
        };
        db.Discounts.Add(d);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(d.Id, ct), null);
    }
    public async Task<(bool Found, string? ValidationError)> UpdateAsync(
        Guid id, UpdateDiscountRequest req, CancellationToken ct = default)
    {
        var d = await db.Discounts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return (false, null);
        if (req.Percent is < 0 or > 100) return (true, "Процент скидки должен быть в диапазоне 0..100");
        if (req.ValidFromUtc is not null && req.ValidToUtc is not null && req.ValidToUtc < req.ValidFromUtc)
            return (true, "Дата окончания раньше даты начала");

        d.Percent = req.Percent;
        d.ValidFromUtc = req.ValidFromUtc;
        d.ValidToUtc = req.ValidToUtc;
        d.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var d = await db.Discounts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return false;
        db.Discounts.Remove(d);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ApplicableDiscount>> GetActiveForAsync(
        IReadOnlyCollection<Guid> vendorIds, IReadOnlyCollection<string> manufacturers, CancellationToken ct = default)
    {
        if (vendorIds.Count == 0 && manufacturers.Count == 0) return [];

        var now = DateTime.UtcNow;
        return await db.Discounts.AsNoTracking()
            .Where(d => (d.ValidFromUtc == null || d.ValidFromUtc <= now) && (d.ValidToUtc == null || d.ValidToUtc >= now))
            .Where(d => (d.VendorId != null && vendorIds.Contains(d.VendorId.Value))
                     || (d.Manufacturer != null && manufacturers.Contains(d.Manufacturer)))
            .Select(d => new ApplicableDiscount(d.VendorId, d.Manufacturer, d.Percent))
            .ToListAsync(ct);
    }

    /// <summary>Проверка цели: ровно одна из (VendorId, Manufacturer). Возвращает нормализованные значения.</summary>
    private static (bool Ok, string? Error, Guid? VendorId, string? Manufacturer) ValidateTarget(Guid? vendorId, string? manufacturer)
    {
        var m = string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer.Trim();
        var hasVendor = vendorId is not null && vendorId != Guid.Empty;
        var hasMfr = m is not null;

        if (hasVendor && hasMfr) return (false, "Укажите только одну цель: поставщика ИЛИ производителя", null, null);
        if (!hasVendor && !hasMfr) return (false, "Укажите цель скидки: поставщика или производителя", null, null);
        if (m?.Length > 300) return (false, "Производитель слишком длинный (макс. 300 символов)", null, null);
        return (true, null, hasVendor ? vendorId : null, m);
    }
}
