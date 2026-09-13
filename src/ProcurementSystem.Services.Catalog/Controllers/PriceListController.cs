using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Catalog.Catalog;
using ProcurementSystem.Services.Catalog.Import;
using ProcurementSystem.Services.Catalog.Pricing;
using ProcurementSystem.Services.Catalog.Seeding;

namespace ProcurementSystem.Services.Catalog.Controllers;

[ApiController]
[Route("api/pricelist")]
[Authorize(Policy = "read")]
public class PriceListController(IOfferService offers, IPriceListImporter importer, IPriceHistoryService history, IPriceListCorpus corpus) : ControllerBase
{
    /// <summary>Все офферы (предложения поставщиков) по конкретному товару.</summary>
    [HttpGet("by-product/{productId:guid}")]
    public Task<IReadOnlyList<OfferDto>> ByProduct(Guid productId, CancellationToken ct)
        => offers.ByProductAsync(productId, ct);

    /// <summary>История цен товара (точки фиксируются при каждом изменении оффера).</summary>
    [HttpGet("history/{productId:guid}")]
    public Task<IReadOnlyList<PriceHistoryDto>> History(Guid productId, CancellationToken ct)
        => history.GetForProductAsync(productId, ct);

    [HttpPost]
    [Authorize(Policy = "write")]
    public async Task<ActionResult<OfferDto>> Create(CreateOfferRequest req, CancellationToken ct)
        => await offers.CreateAsync(req, ct) is { } dto ? Ok(dto) : BadRequest("Товар или поставщик не найден");

    /// <summary>
    /// Импорт прайс-листа поставщика из Excel (xlsx).
    /// Колонки распознаются по заголовкам (гибкий маппинг): «Артикул», «Наименование»/«Номенклатура»,
    /// «Цена» (приоритет «без НДС»); опционально «Производитель», «Категория», «Срок», «Остаток».
    /// Файл целиком архивируется (ТЗ п.5.1) — см. GET .../uploads.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = "write")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(67_108_864)]
    [RequestFormLimits(MultipartBodyLengthLimit = 67_108_864)]
    public async Task<ActionResult<ImportResult>> Import(
        [FromQuery] Guid? vendorId,
        [FromForm] PriceImportForm form,
        CancellationToken ct)
    {
        var (vendorIdForm, file) = (form.VendorId, form.File);
        var vid = vendorId is { } q && q != Guid.Empty ? q : vendorIdForm;
        if (vid is null || vid == Guid.Empty)
            return BadRequest("Укажите поставщика (vendorId).");
        if (file is null || file.Length == 0)
            return BadRequest("Приложите файл Excel (.xlsx или .xls) полем file.");
        await using var stream = file.OpenReadStream();
        return Ok(await importer.ImportAsync(vid.Value, stream, file.FileName, User.Identity?.Name, ct));
    }

    /// <summary>
    /// Стенд ML: 100+ пересекающихся прайсов и каталог на тысячи SKU.
    /// Не парсит чужие сайты. Идемпотентно. Query: lists=120 (20…160), catalogSize=4000 (80…8000).
    /// Кривая заявка (Specification) сюда не входит — quoting; в ответе SpecificationId = Empty.
    /// </summary>
    [HttpPost("seed-corpus")]
    [Authorize(Policy = "admin")]
    public Task<PriceListCorpusResult> SeedCorpus(int lists = 120, int catalogSize = 4000, CancellationToken ct = default)
        => corpus.SeedAsync(lists, catalogSize, ct);

    [HttpGet("uploads")]
    public Task<IReadOnlyList<PriceImportUploadDto>> Uploads([FromQuery] Guid? vendorId, int limit = 50, CancellationToken ct = default)
        => importer.ListUploadsAsync(vendorId, limit, ct);

    /// <summary>Скачать оригинальный файл конкретной загрузки из архива.</summary>
    [HttpGet("uploads/{id:guid}/download")]
    public async Task<IActionResult> DownloadUpload(Guid id, CancellationToken ct)
        => await importer.GetUploadFileAsync(id, ct) is { } f
            ? File(f.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", f.FileName)
            : NotFound();
}

/// <summary>
/// Поля формы импорта прайса одним объектом.
/// Swashbuckle падает на методе, где [FromForm] стоит на простом параметре рядом с IFormFile,
/// и вместе с этой операцией рушится весь документ OpenAPI — интерактивная спецификация
/// открывается пустой. Привязка не меняется: vendorId → VendorId, file → File.
/// </summary>
public class PriceImportForm
{
    public Guid? VendorId { get; set; }
    public IFormFile? File { get; set; }
}
