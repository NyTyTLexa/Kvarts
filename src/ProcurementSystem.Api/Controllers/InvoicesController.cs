using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Commercial;

namespace ProcurementSystem.Api.Controllers;

/// <summary>
/// Счета NOC: создание из согласованного КП и продвижение по стадиям ЖЦ (ТЗ п.B7).
/// Стадии «Пришёл на склад» и «Отражено в 1С» выставляет Трек B через POST .../status.
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize(Policy = "read")]
public class InvoicesController(IInvoiceQueryService queries, IInvoiceCommandService invoices) : ControllerBase
{
    /// <summary>Список счетов NOC.</summary>
    [HttpGet]
    public Task<IReadOnlyList<InvoiceDto>> List(CancellationToken ct) => queries.ListAsync(ct);

    /// <summary>Карточка счёта с позициями (снимок согласованного варианта КП, ТЗ п.6.1).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDetailDto>> Get(Guid id, CancellationToken ct)
        => await queries.GetAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Создать счёт из согласованного КП (сумма/контрагент переносятся из согласования,
    /// позиции фиксируются снимком согласованного варианта КП).</summary>
    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<InvoiceDetailDto>> Create(CreateInvoiceRequest req, CancellationToken ct)
    {
        var (dto, error, conflict) = await invoices.CreateAsync(req, User.Identity?.Name, ct);
        if (error is not null) return conflict ? Conflict(error) : BadRequest(error);
        return CreatedAtAction(nameof(Get), new { id = dto!.Id }, dto);
    }

    /// <summary>Продвинуть счёт по стадиям ЖЦ (ТЗ 10.11): КБ ставит «Согласован», бухгалтерия —
    /// последующие; недопустимый переход — 409. Склад идёт мимо HTTP (WarehouseService).</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "setInvoiceStatus")]
    public async Task<IActionResult> SetStatus(Guid id, SetInvoiceStatusRequest req, CancellationToken ct)
    {
        var actor = new InvoiceStatusActor(
            User.IsInRole("admin") || User.IsInRole("commercial"),
            User.IsInRole("admin") || User.IsInRole("accounting"));
        var r = await invoices.SetStatusAsync(id, req, actor, ct);
        if (!r.Found) return NotFound();
        if (r.Forbidden) return StatusCode(StatusCodes.Status403Forbidden, r.Error);
        if (!r.Ok) return Conflict(r.Error);
        return NoContent();
    }

    /// <summary>Загрузить прикреплённый документ к счёту (ТЗ п.6.1, УК-08 — Бухгалтерия).</summary>
    [HttpPost("{id:guid}/attachments")]
    [Authorize(Policy = "postPayment")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<InvoiceAttachmentDto>> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Файл пустой");
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var dto = await invoices.UploadAttachmentAsync(id, file.FileName, file.ContentType, ms.ToArray(), User.Identity?.Name, ct);
        return dto is null ? NotFound("Счёт не найден") : Ok(dto);
    }

    /// <summary>Список прикреплённых документов счёта.</summary>
    [HttpGet("{id:guid}/attachments")]
    public Task<IReadOnlyList<InvoiceAttachmentDto>> ListAttachments(Guid id, CancellationToken ct)
        => queries.ListAttachmentsAsync(id, ct);

    /// <summary>Скачать оригинал прикреплённого документа.</summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct)
        => await queries.GetAttachmentFileAsync(attachmentId, ct) is { } f
            ? File(f.Content, f.ContentType, f.FileName)
            : NotFound();
}
