using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Retail.Retail;

namespace ProcurementSystem.Services.Retail.Controllers;

/// <summary>
/// Розничные витрины: найти магазины по запросу, снять цену с публичной карточки, записать в каталог.
/// Отдельный контур от B2B-прайсов. Номенклатура и цены — HTTP у Catalog, не локальная копия.
/// </summary>
[ApiController]
[Route("api/retail")]
[Authorize(Policy = "read")]
public class RetailController(IRetailSearchService search, IRetailImportService import) : ControllerBase
{
    [HttpGet("shops")]
    public Task<IReadOnlyList<RetailShopDto>> Shops(CancellationToken ct) => search.ListShopsAsync(ct);

    [HttpPost("search")]
    public Task<RetailSearchResult> Search([FromBody] RetailSearchRequest req, CancellationToken ct)
        => search.SearchAsync(req, ct);

    [HttpPost("import")]
    [Authorize(Policy = "write")]
    public Task<RetailImportResult> Import([FromBody] RetailImportRequest req, CancellationToken ct)
        => import.ImportAsync(req.Hits ?? [], ct);
}
