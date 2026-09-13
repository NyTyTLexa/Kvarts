using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Infrastructure.Commercial;

public interface IApprovalService
{
    Task<IReadOnlyList<ApprovalDto>> ListAsync(CancellationToken ct = default);
    Task<ApprovalDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(ApprovalDto? Dto, string? Error)> CreateAsync(CreateApprovalRequest req, string? createdBy, CancellationToken ct = default);
    Task<(bool Found, string? Error)> SetMarginAsync(Guid id, SetMarginRequest req, CancellationToken ct = default);
    Task<(bool Found, string? Error)> DecideAsync(Guid id, DecisionRequest req, CancellationToken ct = default);

    /// <summary>Вернуть отклонённое согласование на доработку (ТЗ п.5.6: «при отклонении процесс
    /// возвращается на шаг назад с сохранением комментария»).</summary>
    Task<(bool Found, string? Error)> ResubmitAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Согласование КП в коммерческом блоке (ТЗ п.B6). Себестоимость здесь не считается —
/// переиспользуется <see cref="IQuoteGenerator"/>: CostPrice = quote.TotalCost,
/// Title = quote.SpecificationTitle, Customer — из спецификации. Наценка вводится РП/КБ,
/// цена клиенту и маржинальность пересчитываются по формулам ТЗ.
/// </summary>
public class ApprovalService(AppDbContext db, IQuoteGenerator quotes) : IApprovalService
{
    public async Task<IReadOnlyList<ApprovalDto>> ListAsync(CancellationToken ct = default) =>
        await db.Set<Approval>().AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => Map(a))
            .ToListAsync(ct);

    public async Task<ApprovalDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<Approval>().AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => Map(a))
            .FirstOrDefaultAsync(ct);

    public async Task<(ApprovalDto? Dto, string? Error)> CreateAsync(
        CreateApprovalRequest req, string? createdBy, CancellationToken ct = default)
    {
        if (req.MarkupPercent < 0) return (null, "Наценка не может быть отрицательной");

        var spec = await db.Specifications.AsNoTracking()
            .Where(s => s.Id == req.SpecificationId)
            .Select(s => new { s.Customer })
            .FirstOrDefaultAsync(ct);
        if (spec is null) return (null, "Спецификация не найдена");

        // Себестоимость — из генератора КП (сам генератор не меняем).
        var quote = await quotes.GenerateAsync(req.SpecificationId, req.Strategy, ct: ct);

        var a = new Approval
        {
            SpecificationId = req.SpecificationId,
            Strategy = req.Strategy.ToString(),
            Title = quote.SpecificationTitle,
            Customer = spec.Customer,
            CostPrice = quote.TotalCost,
            MarkupPercent = req.MarkupPercent,
            SellPrice = RecalcSell(quote.TotalCost, req.MarkupPercent),
            MarginPercent = RecalcMargin(quote.TotalCost, req.MarkupPercent),
            Status = ApprovalStatus.НаСогласованииРП,
            CreatedBy = createdBy
        };
        db.Set<Approval>().Add(a);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(a.Id, ct), null);
    }

    public async Task<(bool Found, string? Error)> SetMarginAsync(
        Guid id, SetMarginRequest req, CancellationToken ct = default)
    {
        if (req.MarkupPercent < 0) return (true, "Наценка не может быть отрицательной");
        var a = await db.Set<Approval>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (IsDecided(a.Status)) return (true, "Решение по согласованию уже принято");

        a.MarkupPercent = req.MarkupPercent;
        a.SellPrice = RecalcSell(a.CostPrice, req.MarkupPercent);
        a.MarginPercent = RecalcMargin(a.CostPrice, req.MarkupPercent);
        a.Status = ApprovalStatus.ВКоммерческомБлоке;
        a.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Found, string? Error)> DecideAsync(
        Guid id, DecisionRequest req, CancellationToken ct = default)
    {
        var a = await db.Set<Approval>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (IsDecided(a.Status)) return (true, "Решение по согласованию уже принято");

        a.Status = req.Approved ? ApprovalStatus.Согласовано : ApprovalStatus.Отклонено;
        a.DecidedAtUtc = DateTime.UtcNow;
        a.Comment = req.Comment;
        a.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Found, string? Error)> ResubmitAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.Set<Approval>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.Status != ApprovalStatus.Отклонено)
            return (true, "Повторно отправить можно только отклонённое согласование");

        // Comment намеренно не стираем — предыдущее решение остаётся видно РП до нового Decide.
        a.Status = ApprovalStatus.НаСогласованииРП;
        a.DecidedAtUtc = null;
        a.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    private static decimal RecalcSell(decimal cost, decimal markup) => cost * (1m + markup / 100m);
    private static decimal RecalcMargin(decimal cost, decimal markup)
    {
        var sell = RecalcSell(cost, markup);
        return sell > 0 ? (sell - cost) / sell * 100m : 0m;
    }
    private static bool IsDecided(ApprovalStatus s) => s is ApprovalStatus.Согласовано or ApprovalStatus.Отклонено;

    private static ApprovalDto Map(Approval a) => new(
        a.Id, a.SpecificationId, a.Strategy, a.Title, a.Customer,
        a.CostPrice, a.MarkupPercent, a.SellPrice, a.MarginPercent,
        a.Status, a.CreatedBy, a.CreatedAtUtc, a.DecidedAtUtc, a.Comment);
}
