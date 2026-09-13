using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Catalog.Catalog;
using ProcurementSystem.Services.Catalog.Common;

namespace ProcurementSystem.Services.Catalog.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = "read")]
public class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List(int page = 1, int pageSize = 20, string? search = null, string? category = null, CancellationToken ct = default)
        => products.ListAsync(page, pageSize, search, category, ct);

    /// <summary>Категории каталога (иерархические пути) с количеством позиций — для фильтра.</summary>
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct)
        => products.GetCategoriesAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
        => await products.GetAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Аналоги товара (ТЗ п.5.3): та же нижняя категория иерархии.</summary>
    [HttpGet("{id:guid}/analogs")]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Analogs(Guid id, CancellationToken ct)
        => await products.GetAnalogsAsync(id, ct) is { } list ? Ok(list) : NotFound();

    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest req, CancellationToken ct)
    {
        var dto = await products.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest req, CancellationToken ct)
        => await products.UpdateAsync(id, req, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await products.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
