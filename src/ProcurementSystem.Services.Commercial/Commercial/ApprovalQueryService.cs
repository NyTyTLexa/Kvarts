using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Services.Commercial.Persistence;

namespace ProcurementSystem.Services.Commercial.Commercial;

/// <summary>Чтение согласований КП (CQRS: query-сторона — AsNoTracking + проекции, без мутаций).</summary>
public interface IApprovalQueryService
{
    Task<IReadOnlyList<ApprovalDto>> ListAsync(CancellationToken ct = default);
    Task<ApprovalDto?> GetAsync(Guid id, CancellationToken ct = default);
}

public class ApprovalQueryService(CommercialDbContext db) : IApprovalQueryService
{
    public async Task<IReadOnlyList<ApprovalDto>> ListAsync(CancellationToken ct = default) =>
        await db.Approvals.AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

    public async Task<ApprovalDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Approvals.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.ToDto())
            .FirstOrDefaultAsync(ct);
}
