using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Contracts.Search;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Search;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/search")]
[Authorize(Policy = "read")]
public class SearchController(ISearchEngine search) : ControllerBase
{
    /// <summary>
    /// Мгновенный поиск по каталогу с устойчивостью к опечаткам (fuzzy).
    /// Ищет по индексу Meilisearch, наполняемому событиями через Worker.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<ProductSearchDocument>> Get(string? q, int limit = 20, CancellationToken ct = default)
        => await search.SearchAsync<ProductSearchDocument>(ProductIndexer.Index, q ?? string.Empty, limit, ct);
}
