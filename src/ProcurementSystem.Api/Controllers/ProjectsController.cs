using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Projects;

namespace ProcurementSystem.Api.Controllers;

/// <summary>Проекты (ТЗ п.5.2) — карточка поверх спецификации, статус выводится из Approval/Invoice.</summary>
[ApiController]
[Route("api/projects")]
[Authorize(Policy = "read")]
public class ProjectsController(IProjectService projects) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjectDto>> List(CancellationToken ct) => projects.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken ct)
        => await projects.GetAsync(id, ct) is { } dto ? dto : NotFound();

    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest req, CancellationToken ct)
    {
        var (dto, error) = await projects.CreateAsync(req, ct);
        if (error is not null) return BadRequest(error);
        return CreatedAtAction(nameof(Get), new { id = dto!.Id }, dto);
    }

    /// <summary>Привязать/отвязать проект к спецификации (null — отвязать).</summary>
    [HttpPost("{id:guid}/link")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Link(Guid id, LinkSpecificationRequest req, CancellationToken ct)
        => await projects.LinkSpecificationAsync(id, req.SpecificationId, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await projects.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
