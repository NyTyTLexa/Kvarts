using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/specifications")]
[Authorize(Policy = "read")]
public class SpecificationsController(ISpecificationService specs, ISpecificationImporter importer) : ControllerBase
{
    /// <summary>Список спецификаций (заявок).</summary>
    [HttpGet]
    public Task<IReadOnlyList<SpecificationDto>> List(CancellationToken ct) => specs.ListAsync(ct);

    /// <summary>Спецификация с позициями.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpecificationDetailDto>> Get(Guid id, CancellationToken ct)
        => await specs.GetAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Создать пустую спецификацию.</summary>
    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<SpecificationDto>> Create(CreateSpecificationRequest req, CancellationToken ct)
        => Ok(await specs.CreateAsync(req, ct));

    /// <summary>Добавить позицию. Если ProductId не задан, но есть артикул — сопоставляется по артикулу.</summary>
    [HttpPost("{id:guid}/items")]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<SpecificationItemDto>> AddItem(Guid id, AddSpecificationItemRequest req, CancellationToken ct)
        => await specs.AddItemAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound();

    /// <summary>Удалить позицию спецификации.</summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken ct)
        => await specs.DeleteItemAsync(id, itemId, ct) ? NoContent() : NotFound();

    /// <summary>Удалить спецификацию целиком.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await specs.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Зафиксировать ручной выбор поставщика для позиции спецификации (ТЗ п.5.5).</summary>
    [HttpPost("{id:guid}/items/{itemId:guid}/override")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> SetOverride(Guid id, Guid itemId, SetVendorOverrideRequest req, CancellationToken ct)
        => await specs.SetItemVendorOverrideAsync(id, itemId, req.VendorId, User.Identity?.Name, ct) ? NoContent() : NotFound();

    /// <summary>Снять ручную корректировку поставщика для позиции.</summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}/override")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> ClearOverride(Guid id, Guid itemId, CancellationToken ct)
        => await specs.ClearItemVendorOverrideAsync(id, itemId, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Импорт спецификации из Excel (xlsx). Колонки: A=Артикул, B=Наименование, C=Количество.
    /// Позиции сопоставляются с каталогом по артикулу и fuzzy-поиском по наименованию.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = "write")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<SpecImportResult>> Import(
        [FromQuery] string title, [FromQuery] string? customer, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Файл пустой");
        if (string.IsNullOrWhiteSpace(title)) title = file.FileName;
        await using var stream = file.OpenReadStream();
        return Ok(await importer.ImportAsync(title, customer, stream, ct));
    }
}
