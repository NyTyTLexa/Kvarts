using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Pricing;

namespace ProcurementSystem.Api.Controllers;

/// <summary>
/// Скидки по поставщику/производителю (ТЗ UC-03, п.5.4). Применяются автоматически
/// при формировании КП. Ведёт руководитель проекта (политика write).
/// </summary>
[ApiController]
[Route("api/discounts")]
[Authorize(Policy = "read")]
public class DiscountsController(IDiscountService discounts) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DiscountDto>> List(DiscountTarget? target = null, string? search = null, CancellationToken ct = default)
        => discounts.ListAsync(target, search, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DiscountDto>> Get(Guid id, CancellationToken ct)
        => await discounts.GetAsync(id, ct) is { } dto ? dto : NotFound();

    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<DiscountDto>> Create(CreateDiscountRequest req, CancellationToken ct)
    {
        var (dto, error) = await discounts.CreateAsync(req, User.Identity?.Name, ct);
        if (error is not null) return BadRequest(error);
        return CreatedAtAction(nameof(Get), new { id = dto!.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Update(Guid id, UpdateDiscountRequest req, CancellationToken ct)
    {
        var (found, error) = await discounts.UpdateAsync(id, req, ct);
        if (!found) return NotFound();
        if (error is not null) return BadRequest(error);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await discounts.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
