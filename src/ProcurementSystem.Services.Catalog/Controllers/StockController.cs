using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Catalog.Catalog;

namespace ProcurementSystem.Services.Catalog.Controllers;

/// <summary>Резерв/возврат остатка. Клиент (Ordering) ждёт 2xx и не читает тело.</summary>
[ApiController]
[Route("api/catalog/stock")]
[Authorize(Policy = "write")]
public class StockController(IStockAdjustService stock) : ControllerBase
{
    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustRequest? req, CancellationToken ct)
    {
        await stock.AdjustAsync(req?.Items, ct);
        return NoContent();
    }
}
