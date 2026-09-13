using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Ordering;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = "read")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Список заказов.</summary>
    [HttpGet]
    public Task<IReadOnlyList<OrderDto>> List(CancellationToken ct) => orders.ListAsync(ct);

    /// <summary>Заказ с позициями.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDetailDto>> Get(Guid id, CancellationToken ct)
        => await orders.GetAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Оформить заказ из КП по спецификации (снимок выбранного варианта).</summary>
    [HttpPost("from-quote")]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<OrderDetailDto>> CreateFromQuote(
        [FromQuery] Guid specId,
        [FromQuery] QuoteStrategy strategy = QuoteStrategy.Balanced,
        [FromQuery] double wPrice = 0.5, [FromQuery] double wLead = 0.5, [FromQuery] bool onlyInStock = false,
        CancellationToken ct = default)
    {
        var dto = await orders.CreateFromQuoteAsync(specId, strategy, wPrice, wLead, onlyInStock, User.Identity?.Name, ct);
        return dto is null
            ? BadRequest("В КП нет сопоставленных позиций — заказ не из чего формировать")
            : CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Изменить статус заказа (с проверкой допустимости перехода).</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "write")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeOrderStatusRequest req, CancellationToken ct)
    {
        var r = await orders.ChangeStatusAsync(id, req.Status, ct);
        if (!r.Found) return NotFound();
        if (!r.Ok) return Conflict(r.Error);
        return NoContent();
    }
}
