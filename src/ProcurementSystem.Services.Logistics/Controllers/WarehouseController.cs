using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Logistics.Logistics;

namespace ProcurementSystem.Services.Logistics.Controllers;

/// <summary>
/// Приёмка заказов на склад (ТЗ п.B11, актор — Склад, UC-09): сверка заказанного и принятого,
/// фиксация расхождений, проведение с выгрузкой во внешние системы (WMS/1С, Этап 8).
/// </summary>
[ApiController]
[Route("api/receipts")]
[Authorize(Policy = "read")]
public class WarehouseController(IWarehouseService warehouse) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReceiptDto>> List(CancellationToken ct) => warehouse.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptDetailDto>> Get(Guid id, CancellationToken ct)
        => await warehouse.GetAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Создать приёмку из заказа.</summary>
    [HttpPost("from-order")]
    [Authorize(Policy = "receive")]
    public async Task<ActionResult<ReceiptDetailDto>> CreateFromOrder([FromQuery] Guid orderId, CancellationToken ct)
        => await warehouse.CreateFromOrderAsync(orderId, User.Identity?.Name, ct) is { } dto
            ? CreatedAtAction(nameof(Get), new { id = dto.Id }, dto)
            : NotFound("Заказ не найден");

    /// <summary>Указать фактически принятое количество по строке.</summary>
    [HttpPost("{id:guid}/lines/{lineId:guid}")]
    [Authorize(Policy = "receive")]
    public async Task<IActionResult> SetReceived(Guid id, Guid lineId, SetReceivedRequest req, CancellationToken ct)
    {
        var r = await warehouse.SetReceivedAsync(id, lineId, req.ReceivedQty, ct);
        if (!r.Found) return NotFound();
        if (!r.Ok) return Conflict(r.Error);
        return Ok(r.Receipt);
    }

    /// <summary>Провести приёмку (выгрузка в WMS/1С).</summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "receive")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var r = await warehouse.CompleteAsync(id, ct);
        if (!r.Found) return NotFound();
        if (!r.Ok) return Conflict(r.Error);
        return Ok(r.Receipt);
    }
}
