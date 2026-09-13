using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Search;
using ProcurementSystem.Infrastructure.Seeding;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "admin")]
public class AdminController(IDataSeeder seeder, IProductIndexer indexer) : ControllerBase
{
    /// <summary>Сгенерировать синтетический каталог (поставщики, товары, офферы).</summary>
    [HttpPost("seed")]
    public async Task<ActionResult<SeedResult>> Seed(
        int vendors = 50, int products = 5000, int maxOffersPerProduct = 4, CancellationToken ct = default)
        => Ok(await seeder.SeedAsync(vendors, products, maxOffersPerProduct, ct));

    /// <summary>Полностью очистить каталог.</summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await seeder.ResetAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Полная переиндексация каталога в поисковый движок (массовое наполнение индекса напрямую,
    /// в обход событийного пути — удобно для первичной загрузки после seed).
    /// </summary>
    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex(CancellationToken ct)
        => Ok(new { indexed = await indexer.ReindexAllAsync(ct) });
}
