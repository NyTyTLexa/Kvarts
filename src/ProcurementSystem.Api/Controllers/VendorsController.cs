using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Catalog;
using ProcurementSystem.Infrastructure.Common;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/vendors")]
[Authorize(Policy = "read")]
public class VendorsController(IVendorService vendors) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<VendorDto>> List(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default)
        => vendors.ListAsync(page, pageSize, search, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VendorDto>> Get(Guid id, CancellationToken ct)
        => await vendors.GetAsync(id, ct) is { } dto ? dto : NotFound();

    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<VendorDto>> Create(CreateVendorRequest req, CancellationToken ct)
    {
        var dto = await vendors.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Update(Guid id, UpdateVendorRequest req, CancellationToken ct)
        => await vendors.UpdateAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await vendors.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
