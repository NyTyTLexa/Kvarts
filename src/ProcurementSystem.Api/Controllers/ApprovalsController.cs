using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Commercial;

namespace ProcurementSystem.Api.Controllers;

/// <summary>
/// Согласование КП в коммерческом блоке: маржа и маршрут согласования (ТЗ п.B6).
/// Чтение — для всех аутентифицированных (политика read); мутации — write (admin, manager).
/// CQRS: GET-эндпоинты обслуживает query-сервис, мутации — command-сервис.
/// </summary>
[ApiController]
[Route("api/approvals")]
[Authorize(Policy = "read")]
public class ApprovalsController(IApprovalQueryService queries, IApprovalCommandService approvals) : ControllerBase
{
    /// <summary>Список согласований КП.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ApprovalDto>> List(CancellationToken ct) => queries.ListAsync(ct);

    /// <summary>Карточка согласования.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApprovalDto>> Get(Guid id, CancellationToken ct)
        => await queries.GetAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Создать согласование: себестоимость из КП, наценка РП, статус «На согласовании РП».</summary>
    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<ApprovalDto>> Create(CreateApprovalRequest req, CancellationToken ct)
    {
        var (dto, error) = await approvals.CreateAsync(req, User.Identity?.Name, ct);
        if (error is not null) return BadRequest(error);
        return CreatedAtAction(nameof(Get), new { id = dto!.Id }, dto);
    }

    /// <summary>Задать наценку: пересчёт цены/маржи, статус → «В коммерческом блоке».</summary>
    [HttpPost("{id:guid}/margin")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> SetMargin(Guid id, SetMarginRequest req, CancellationToken ct)
    {
        var (found, error) = await approvals.SetMarginAsync(id, req, ct);
        if (!found) return NotFound();
        if (error is not null) return BadRequest(error);
        return NoContent();
    }

    /// <summary>Решение по согласованию (только коммерческий блок, ТЗ UC-06): «Согласовано» или
    /// «Отклонено», фиксируется время и комментарий.</summary>
    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = "approve")]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest req, CancellationToken ct)
    {
        var (found, error) = await approvals.DecideAsync(id, req, ct);
        if (!found) return NotFound();
        if (error is not null) return BadRequest(error);
        return NoContent();
    }

    /// <summary>Вернуть отклонённое согласование на доработку (ТЗ п.5.6) — снова «На согласовании
    /// РП», комментарий предыдущего решения не стирается.</summary>
    [HttpPost("{id:guid}/resubmit")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Resubmit(Guid id, CancellationToken ct)
    {
        var (found, error) = await approvals.ResubmitAsync(id, ct);
        if (!found) return NotFound();
        if (error is not null) return BadRequest(error);
        return NoContent();
    }
}
