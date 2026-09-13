using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Catalog.Catalog;
using ProcurementSystem.Services.Catalog.Common;

namespace ProcurementSystem.Services.Catalog.Controllers;

/// <summary>Справочник производителей (ТЗ п.5.2).</summary>
[ApiController]
[Route("api/manufacturers")]
[Authorize(Policy = "read")]
public class ManufacturersController(IManufacturerService manufacturers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ManufacturerDto>> List(int page = 1, int pageSize = 50, string? search = null, CancellationToken ct = default)
        => manufacturers.ListAsync(page, pageSize, search, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManufacturerDto>> Get(Guid id, CancellationToken ct)
        => await manufacturers.GetAsync(id, ct) is { } dto ? dto : NotFound();

    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<ManufacturerDto>> Create(CreateManufacturerRequest req, CancellationToken ct)
    {
        var (dto, error) = await manufacturers.CreateAsync(req, ct);
        if (error is not null) return BadRequest(error);
        return CreatedAtAction(nameof(Get), new { id = dto!.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Update(Guid id, UpdateManufacturerRequest req, CancellationToken ct)
        => await manufacturers.UpdateAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await manufacturers.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
