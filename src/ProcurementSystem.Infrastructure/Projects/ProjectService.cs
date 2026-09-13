using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Projects;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Projects;

public record ProjectDto(Guid Id, string Name, string? Rp, DateTime CreatedAtUtc, DateTime? DueDateUtc,
    Guid? SpecificationId, string? SpecificationTitle, int ItemsCount,
    string Status, decimal? Value, decimal? MarginPercent);

public record CreateProjectRequest(string Name, string? Rp, DateTime? DueDateUtc, Guid? SpecificationId);
public record LinkSpecificationRequest(Guid? SpecificationId);

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default);
    Task<ProjectDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(ProjectDto? Dto, string? Error)> CreateAsync(CreateProjectRequest req, CancellationToken ct = default);
    Task<bool> LinkSpecificationAsync(Guid id, Guid? specificationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Проекты (ТЗ п.5.2) — карточка-«обёртка» поверх спецификации. Статус, сумма и маржа не
/// хранятся отдельно — они выводятся живьём из связанных Approval/Invoice (та же сквозная
/// цепочка, что и на дашборде статусов ТЗ раздел 7: Черновик КП → Согласование РП →
/// В коммерческом блоке → КП согласовано → Счёт создан → Ожидание оплаты → Оплачено →
/// Ожидание поставки → Пришёл на склад → Отражено в 1С), чтобы карточка не рассинхронизировалась
/// с реальным ходом дела.
/// </summary>
public class ProjectService(AppDbContext db) : IProjectService
{
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default)
    {
        var projects = await db.Set<Project>().AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        return await AttachAsync(projects, ct);
    }

    public async Task<ProjectDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        return (await AttachAsync([p], ct))[0];
    }

    public async Task<(ProjectDto? Dto, string? Error)> CreateAsync(CreateProjectRequest req, CancellationToken ct = default)
    {
        var name = req.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, "Название проекта обязательно");
        if (req.SpecificationId is { } sid && !await db.Specifications.AnyAsync(s => s.Id == sid, ct))
            return (null, "Спецификация не найдена");

        var p = new Project { Name = name, Rp = req.Rp, DueDateUtc = req.DueDateUtc, SpecificationId = req.SpecificationId };
        db.Add(p);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(p.Id, ct), null);
    }

    public async Task<bool> LinkSpecificationAsync(Guid id, Guid? specificationId, CancellationToken ct = default)
    {
        var p = await db.Set<Project>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (specificationId is { } sid && !await db.Specifications.AnyAsync(s => s.Id == sid, ct))
            return false;
        p.SpecificationId = specificationId;
        p.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Set<Project>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        db.Remove(p);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<List<ProjectDto>> AttachAsync(List<Project> projects, CancellationToken ct)
    {
        var specIds = projects.Where(p => p.SpecificationId != null).Select(p => p.SpecificationId!.Value).Distinct().ToList();

        var specInfo = await db.Specifications.AsNoTracking()
            .Where(s => specIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Title, ItemsCount = s.Items.Count })
            .ToDictionaryAsync(x => x.Id, ct);

        // «Последний» Approval/Invoice по спецификации/согласованию — сведение в памяти,
        // чтобы не зависеть от трансляции GroupBy+First в конкретный SQL-диалект.
        var allApprovals = await db.Set<Approval>().AsNoTracking()
            .Where(a => specIds.Contains(a.SpecificationId)).ToListAsync(ct);
        var apprBySpec = allApprovals.GroupBy(a => a.SpecificationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAtUtc).First());

        var apprIds = apprBySpec.Values.Select(a => a.Id).ToList();
        var allInvoices = await db.Set<Invoice>().AsNoTracking()
            .Where(i => apprIds.Contains(i.ApprovalId)).ToListAsync(ct);
        var invByAppr = allInvoices.GroupBy(i => i.ApprovalId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAtUtc).First());

        return projects.Select(p =>
        {
            specInfo.TryGetValue(p.SpecificationId ?? Guid.Empty, out var spec);
            apprBySpec.TryGetValue(p.SpecificationId ?? Guid.Empty, out var appr);
            Invoice? inv = appr is not null && invByAppr.TryGetValue(appr.Id, out var i) ? i : null;
            var (status, value, margin) = Resolve(appr, inv);

            return new ProjectDto(p.Id, p.Name, p.Rp, p.CreatedAtUtc, p.DueDateUtc,
                p.SpecificationId, spec?.Title, spec?.ItemsCount ?? 0, status, value, margin);
        }).ToList();
    }

    /// <summary>Статус/сумма/маржа проекта — производные от актуального Approval/Invoice
    /// (ТЗ раздел 7: те же названия статусов, что и в жизненном цикле КП/счёта).</summary>
    private static (string Status, decimal? Value, decimal? Margin) Resolve(Approval? appr, Invoice? inv)
    {
        if (appr is null) return ("Черновик КП", null, null);
        if (appr.Status == ApprovalStatus.Отклонено) return ("Отклонено", appr.SellPrice, appr.MarginPercent);
        if (appr.Status == ApprovalStatus.НаСогласованииРП) return ("Согласование РП", appr.SellPrice, appr.MarginPercent);
        if (appr.Status == ApprovalStatus.ВКоммерческомБлоке) return ("В коммерческом блоке", appr.SellPrice, appr.MarginPercent);

        // appr.Status == Согласовано:
        if (inv is null) return ("КП согласовано", appr.SellPrice, appr.MarginPercent);
        var status = inv.Status switch
        {
            InvoiceStatus.Создан or InvoiceStatus.Согласован => "Счёт создан",
            InvoiceStatus.ОжиданиеОплаты or InvoiceStatus.ЧастичнаяОплата => "Ожидание оплаты",
            InvoiceStatus.Оплачено => "Оплачено",
            InvoiceStatus.ОжиданиеПоставки => "Ожидание поставки",
            InvoiceStatus.ПришёлНаСклад => "Пришёл на склад",
            InvoiceStatus.ОтраженоВ1С => "Отражено в 1С",
            InvoiceStatus.Отменён => "Отклонено",
            _ => "КП согласовано"
        };
        return (status, inv.SellPrice, appr.MarginPercent);
    }
}
