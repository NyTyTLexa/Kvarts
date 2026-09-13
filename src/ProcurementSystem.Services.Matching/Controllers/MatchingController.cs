using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Matching.Matching;

namespace ProcurementSystem.Services.Matching.Controllers;

/// <summary>
/// ML-сопоставление спецификации с каталогом (ТЗ п.5.3): TF-IDF + логистическая регрессия,
/// аналоги по категории, затем ручное подтверждение или автоприменение лучших гипотез.
/// </summary>
[ApiController]
[Route("api/specifications/{specId:guid}/match")]
[Route("api/matching/{specId:guid}")]
[Authorize(Policy = "read")]
public class MatchingController(IMatchingService matching) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SpecMatchDto>> Preview(Guid specId, CancellationToken ct)
        => await matching.PreviewAsync(specId, ct) is { } dto ? dto : NotFound();

    /// <summary>Проставить ProductId по лучшей гипотезе с P ≥ порога (по умолчанию 0.5).</summary>
    [HttpPost("apply")]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<SpecMatchDto>> ApplyBest(Guid specId, [FromQuery] double minP = 0.5, CancellationToken ct = default)
        => await matching.ApplyBestAsync(specId, minP, ct) is { } dto ? dto : NotFound();

    /// <summary>Вручную привязать позицию к товару из подсказок модели.</summary>
    [HttpPost("apply-one")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> ApplyOne(Guid specId, ApplyMatchItemRequest req, CancellationToken ct)
        => await matching.ApplyOneAsync(specId, req.ItemId, req.ProductId, ct) ? NoContent() : NotFound();
}
