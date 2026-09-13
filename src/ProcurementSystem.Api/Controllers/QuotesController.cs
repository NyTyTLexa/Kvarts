using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Api.Controllers;

[ApiController]
[Route("api/specifications/{specId:guid}/quote")]
[Authorize(Policy = "read")]
public class QuotesController(IQuoteGenerator generator, IQuoteExcelExporter exporter, IQuotePdfExporter pdfExporter) : ControllerBase
{
    /// <summary>Сгенерировать КП по выбранной стратегии (по умолчанию — сбалансированной).</summary>
    [HttpGet]
    public async Task<ActionResult<Quote>> Generate(
        Guid specId,
        QuoteStrategy strategy = QuoteStrategy.Balanced,
        double wPrice = 0.5, double wLead = 0.5, bool onlyInStock = false,
        CancellationToken ct = default)
    {
        try { return Ok(await generator.GenerateAsync(specId, strategy, wPrice, wLead, onlyInStock, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Сравнить варианты КП: мин. стоимость / мин. срок / баланс / ML-актуальность.</summary>
    [HttpGet("compare")]
    public async Task<ActionResult<IReadOnlyList<Quote>>> Compare(
        Guid specId, double wPrice = 0.5, double wLead = 0.5, bool onlyInStock = false, CancellationToken ct = default)
    {
        try { return Ok(await generator.GenerateAllAsync(specId, wPrice, wLead, onlyInStock, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Выгрузить КП в Excel (xlsx).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        Guid specId, QuoteStrategy strategy = QuoteStrategy.Balanced,
        double wPrice = 0.5, double wLead = 0.5, bool onlyInStock = false, CancellationToken ct = default)
    {
        try
        {
            var quote = await generator.GenerateAsync(specId, strategy, wPrice, wLead, onlyInStock, ct);
            var bytes = exporter.Export(quote);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"quote_{specId}_{strategy}.xlsx");
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Выгрузить КП в PDF (ТЗ: «желательно PDF», подтверждено заказчиком).</summary>
    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        Guid specId, QuoteStrategy strategy = QuoteStrategy.Balanced,
        double wPrice = 0.5, double wLead = 0.5, bool onlyInStock = false, CancellationToken ct = default)
    {
        try
        {
            var quote = await generator.GenerateAsync(specId, strategy, wPrice, wLead, onlyInStock, ct);
            return File(pdfExporter.Export(quote), "application/pdf", $"quote_{specId}_{strategy}.pdf");
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }
}
