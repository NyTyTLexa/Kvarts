using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Services.Matching.Matching;

namespace ProcurementSystem.Services.Matching.Controllers;

/// <summary>Подсказки модели без заявки: живой curl и произвольная строка номенклатуры.</summary>
[ApiController]
[Route("api/matching")]
[Authorize(Policy = "read")]
public class SuggestController(IMatchingService matching) : ControllerBase
{
    [HttpPost("suggest")]
    public async Task<ActionResult<IReadOnlyList<MatchSuggestion>>> SuggestPost(SuggestMatchRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("name обязателен");
        var take = Math.Clamp(req.Take, 1, 15);
        return Ok(await matching.SuggestAsync(req.Sku, req.Name, take, ct));
    }

    [HttpGet("suggest")]
    public async Task<ActionResult<IReadOnlyList<MatchSuggestion>>> SuggestGet(
        [FromQuery] string? sku, [FromQuery] string? name, [FromQuery] int take = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("name обязателен");
        take = Math.Clamp(take, 1, 15);
        return Ok(await matching.SuggestAsync(sku, name, take, ct));
    }
}
