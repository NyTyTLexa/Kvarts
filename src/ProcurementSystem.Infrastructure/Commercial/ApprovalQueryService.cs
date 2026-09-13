using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Commercial;

/// <summary>Чтение согласований КП (CQRS: query-сторона — AsNoTracking + проекции, без мутаций).</summary>
public interface IApprovalQueryService
{
    Task<IReadOnlyList<ApprovalDto>> ListAsync(CancellationToken ct = default);
    Task<ApprovalDto?> GetAsync(Guid id, CancellationToken ct = default);
}

public class ApprovalQueryService(AppDbContext db) : IApprovalQueryService
{
    public async Task<IReadOnlyList<ApprovalDto>> ListAsync(CancellationToken ct = default) =>
        await db.Set<Approval>().AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

    public async Task<ApprovalDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<Approval>().AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.ToDto())
            .FirstOrDefaultAsync(ct);
}
