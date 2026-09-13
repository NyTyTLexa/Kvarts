using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Catalog;
using ProcurementSystem.Infrastructure.Common;

namespace ProcurementSystem.Api.Controllers;

/// <summary>Электронный каталог: витрина номенклатуры с ценами поставщиков.</summary>
[ApiController]
[Route("api/catalog")]
[Authorize(Policy = "read")]
public class CatalogController(ICatalogBrowseService catalog) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CatalogCardDto>> Browse(
        int page = 1,
        int pageSize = 24,
        string? search = null,
        string? category = null,
        string? manufacturer = null,
        Guid? vendorId = null,
        bool inStockOnly = false,
        string? sort = "name",
        CancellationToken ct = default)
        => catalog.BrowseAsync(page, pageSize, search, category, manufacturer, vendorId, inStockOnly, sort, ct);

    /// <summary>Подсказки поиска по артикулу, имени и бренду.</summary>
    [HttpGet("suggest")]
    public Task<IReadOnlyList<CatalogSuggestDto>> Suggest(string? q, int limit = 8, CancellationToken ct = default)
        => catalog.SuggestAsync(q, limit, ct);

    /// <summary>Карточки для сравнения / избранного. Query: ids=guid,guid (до 48).</summary>
    [HttpGet("compare")]
    public Task<IReadOnlyList<CatalogCardDto>> Compare([FromQuery] string? ids, CancellationToken ct = default)
        => catalog.CompareAsync(ParseIds(ids), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CatalogProductDetailDto>> Get(Guid id, CancellationToken ct)
        => await catalog.GetAsync(id, ct) is { } dto ? dto : NotFound();

    private static IReadOnlyList<Guid> ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return [];
        var list = new List<Guid>();
        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Guid.TryParse(part, out var g)) list.Add(g);
        return list;
    }
}
