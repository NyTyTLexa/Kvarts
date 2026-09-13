using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Audit;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "admin")]
public class AuditController(IAuditService audit) : ControllerBase
{
    /// <summary>Последние записи журнала действий (только администратор).</summary>
    [HttpGet]
    public Task<IReadOnlyList<AuditEntryDto>> Recent(int limit = 100, CancellationToken ct = default)
        => audit.RecentAsync(limit, ct);
}
