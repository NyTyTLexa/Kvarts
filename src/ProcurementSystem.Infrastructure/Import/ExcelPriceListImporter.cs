using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Import;
using ProcurementSystem.Infrastructure.Outbox;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Pricing;

namespace ProcurementSystem.Infrastructure.Import;

public record ImportResult(int RowsProcessed, int ProductsCreated, int OffersUpserted, IReadOnlyList<string> Errors);

public record PriceImportUploadDto(Guid Id, Guid VendorId, string VendorName, string FileName,
    DateTime UploadedAtUtc, string? UploadedBy, int RowsProcessed, int ProductsCreated,
    int OffersUpserted, int ErrorsCount);

public interface IPriceListImporter
{
    Task<ImportResult> ImportAsync(Guid vendorId, Stream excel, string fileName, string? uploadedBy, CancellationToken ct = default);

    /// <summary>История загрузок прайсов (ТЗ п.5.1 — архив, версии) по поставщику или по всем.</summary>
    Task<IReadOnlyList<PriceImportUploadDto>> ListUploadsAsync(Guid? vendorId, int limit, CancellationToken ct = default);

    /// <summary>Скачать оригинальный файл загрузки из архива.</summary>
    Task<(byte[] Content, string FileName)?> GetUploadFileAsync(Guid uploadId, CancellationToken ct = default);
}

/// <summary>
/// Импорт прайс-листа поставщика из Excel с гибким маппингом колонок (ТЗ, раздел рисков:
/// «Разработать гибкий маппинг колонок»). Строка заголовков ищется автоматически в первых
/// 30 строках (реальные прайсы — например, EKF — начинаются с шапки на 10+ строк с логотипом
/// и контактами), колонки распознаются по именам: «Артикул», «Наименование»/«Номенклатура»,
/// «Цена» (приоритет — колонке «без НДС»: НДС добавляет генератор КП, иначе он задвоится),
/// опционально «Производитель», «Категория», «Срок…», «Остаток» (фолбэки: null, null,
/// срок поставщика по умолчанию, 0).
/// Товар ищется/создаётся по артикулу (SKU), оффер для (поставщик, товар) — upsert.
/// Сохранение — пакетами (реальные прайсы бывают на 20 000+ строк, построчный SaveChanges
/// растягивал бы импорт на минуты). Каждая загрузка архивируется целиком (ТЗ п.5.1).
/// </summary>
public class ExcelPriceListImporter(AppDbContext db) : IPriceListImporter
{
    private const int MaxDetailedErrors = 100;   // не раздуваем ответ на файлах с тысячами ошибок
    private const int SaveBatchSize = 500;
    private const int MaxCategoryLength = 300;

    private sealed record ColumnMap(int HeaderRow, int Sku, int Name, int? Manufacturer, int? Category, int Price, int? LeadTime, int? Stock);
    private sealed record ParsedRow(int RowNumber, string Sku, string Name, string? Manufacturer, string? Category, decimal Price, int LeadTimeDays, int Stock);

    public async Task<ImportResult> ImportAsync(Guid vendorId, Stream excel, string fileName, string? uploadedBy, CancellationToken ct = default)
    {
        var vendor = await db.Vendors.FindAsync([vendorId], ct);
        if (vendor is null)
            return new ImportResult(0, 0, 0, [$"Поставщик {vendorId} не найден"]);

        // Буферизуем сначала — исходные байты нужны и парсеру, и архиву, а Stream от IFormFile
        // не всегда перематывается назад.
        using var buffer = new MemoryStream();
        await excel.CopyToAsync(buffer, ct);
        var rawBytes = buffer.ToArray();

        var errors = new List<string>();
        var hiddenErrors = 0;
        void AddError(string msg) { if (errors.Count < MaxDetailedErrors) errors.Add(msg); else hiddenErrors++; }

        int rows = 0, created = 0, upserted = 0;
        var parsed = new List<ParsedRow>();
        var hierarchy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var wb = LoadWorkbook(rawBytes);

            IXLWorksheet? ws = null;
            ColumnMap? map = null;
            foreach (var sheet in wb.Worksheets)
            {
                var detected = DetectColumns(sheet);
                if (detected is null) continue;
                ws = sheet;
                map = detected;
                break;
            }

            if (ws is null || map is null)
            {
                AddError("Структура файла не распознана: не найдена строка заголовков " +
                         "(ожидаются колонки «Артикул», «Наименование»/«Номенклатура» и «Цена»)");
            }
            else
            {
                // Иерархия номенклатуры (ТЗ п.5.3): реальные прайсы (EKF) держат колонки
                // «1–4 уровень иерархии» на вспомогательных листах — собираем sku → путь.
                foreach (var extra in wb.Worksheets)
                {
                    if (extra == ws) continue;
                    CollectHierarchy(extra, hierarchy);
                }

                // Дублирующиеся артикулы в одном файле (ТЗ п.10): не блокируем загрузку, но фиксируем
                // предупреждением — иначе повтор молча перезаписывает предыдущую строку без следа.
                var seenSkuRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in ws.RowsUsed())
                {
                    ct.ThrowIfCancellationRequested();
                    if (row.RowNumber() <= map.HeaderRow) continue;
                    var sku = row.Cell(map.Sku).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(sku)) continue;   // пустые/групповые строки
                    rows++;

                    if (seenSkuRows.TryGetValue(sku, out var firstRow))
                        AddError($"Строка {row.RowNumber()}: артикул «{sku}» повторяет строку {firstRow} — применена последняя цена");
                    else
                        seenSkuRows[sku] = row.RowNumber();

                    if (!TryReadDecimal(row.Cell(map.Price), out var price))
                    {
                        AddError($"Строка {row.RowNumber()}: не удалось прочитать цену «{row.Cell(map.Price).GetString().Trim()}»");
                        continue;
                    }

                    var name = row.Cell(map.Name).GetString().Trim();
                    parsed.Add(new ParsedRow(
                        row.RowNumber(), sku,
                        string.IsNullOrWhiteSpace(name) ? sku : name,
                        ReadOptionalString(row, map.Manufacturer),
                        ReadOptionalString(row, map.Category),
                        price,
                        ReadOptionalInt(row, map.LeadTime) ?? vendor.DefaultLeadTimeDays,
                        ReadOptionalInt(row, map.Stock) ?? 0));
                }
            }
        }
        catch (Exception ex)
        {
            // Некорректная структура файла целиком (не читается как xlsx, нет листов и т.п.) —
            // явная ошибка структуры, а не построчная (ТЗ п.5.1).
            AddError($"Структура файла не распознана: {ex.Message}");
        }

        if (parsed.Count > 0)
        {
            // Пакетная запись: все существующие товары/офферы — двумя запросами, сохранение — партиями.
            var skus = parsed.Select(p => p.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var products = (await db.Products.Where(p => skus.Contains(p.Sku)).ToListAsync(ct))
                .ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
            var offers = (await db.PriceListItems.Where(o => o.VendorId == vendorId).ToListAsync(ct))
                .ToDictionary(o => o.ProductId);

            var pending = 0;
            foreach (var r in parsed)
            {
                ct.ThrowIfCancellationRequested();
                if (!products.TryGetValue(r.Sku, out var product))
                {
                    product = new Product
                    {
                        Sku = r.Sku,
                        Name = r.Name,
                        Manufacturer = r.Manufacturer,
                        Category = MakeCategorySafeForStorage(r.Category)
                    };
                    db.Products.Add(product);
                    products[r.Sku] = product;
                    created++;
                }
                if (hierarchy.TryGetValue(r.Sku, out var path))
                    product.Category = MakeCategorySafeForStorage(path);

                if (!offers.TryGetValue(product.Id, out var offer))
                {
                    offer = new PriceListItem { VendorId = vendorId, ProductId = product.Id };
                    db.PriceListItems.Add(offer);
                    offers[product.Id] = offer;
                }
                offer.Price = r.Price;
                offer.Currency = "RUB";
                offer.LeadTimeDays = r.LeadTimeDays;
                offer.StockQuantity = r.Stock;
                offer.UpdatedAtUtc = DateTime.UtcNow;
                db.EnqueueEvent(new ProductUpserted(product.Id, DateTime.UtcNow));
                db.RecordPriceChange(product.Id, vendorId, r.Price, r.LeadTimeDays);
                upserted++;

                if (++pending >= SaveBatchSize)
                {
                    await db.SaveChangesAsync(ct);
                    pending = 0;
                }
            }
            if (pending > 0) await db.SaveChangesAsync(ct);
        }

        if (hiddenErrors > 0)
            errors.Add($"…и ещё {hiddenErrors} сообщений скрыто (показаны первые {MaxDetailedErrors})");

        db.Add(new PriceImportUpload
        {
            VendorId = vendorId,
            VendorName = vendor.Name,
            FileName = fileName,
            FileContent = rawBytes,
            UploadedBy = uploadedBy,
            RowsProcessed = rows,
            ProductsCreated = created,
            OffersUpserted = upserted,
            ErrorsCount = errors.Count,
            ErrorsSummary = errors.Count == 0 ? null : string.Join("; ", errors.Take(5)),
        });
        await db.SaveChangesAsync(ct);

        return new ImportResult(rows, created, upserted, errors);
    }

    public async Task<IReadOnlyList<PriceImportUploadDto>> ListUploadsAsync(Guid? vendorId, int limit, CancellationToken ct = default)
    {
        var q = db.Set<PriceImportUpload>().AsNoTracking();
        if (vendorId is not null) q = q.Where(u => u.VendorId == vendorId);
        return await q.OrderByDescending(u => u.CreatedAtUtc).Take(Math.Clamp(limit, 1, 200))
            .Select(u => new PriceImportUploadDto(u.Id, u.VendorId, u.VendorName, u.FileName,
                u.CreatedAtUtc, u.UploadedBy, u.RowsProcessed, u.ProductsCreated, u.OffersUpserted, u.ErrorsCount))
            .ToListAsync(ct);
    }

    public async Task<(byte[] Content, string FileName)?> GetUploadFileAsync(Guid uploadId, CancellationToken ct = default)
    {
        var u = await db.Set<PriceImportUpload>().AsNoTracking()
            .Where(x => x.Id == uploadId).Select(x => new { x.FileContent, x.FileName }).FirstOrDefaultAsync(ct);
        return u is null ? null : (u.FileContent, u.FileName);
    }

    /// <summary>
    /// Открывает файл прайса любого поддерживаемого формата. Современный .xlsx (zip, «PK») читается
    /// ClosedXML напрямую; старый бинарный .xls (OLE2-сигнатура) — через NPOI, значения первого листа
    /// переливаются в in-memory книгу ClosedXML, чтобы дальше работал один общий конвейер маппинга.
    /// </summary>
    private static XLWorkbook LoadWorkbook(byte[] bytes)
    {
        var isOle2 = bytes.Length >= 8 &&
                     bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0;
        if (!isOle2)
            return new XLWorkbook(new MemoryStream(bytes));

        using var stream = new MemoryStream(bytes);
        using var legacy = new NPOI.HSSF.UserModel.HSSFWorkbook(stream);
        var wb = new XLWorkbook();
        for (var sheetIndex = 0; sheetIndex < legacy.NumberOfSheets; sheetIndex++)
        {
            var sheet = legacy.GetSheetAt(sheetIndex);
            var ws = wb.AddWorksheet(sheet.SheetName);
            for (var r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null || row.FirstCellNum < 0) continue;
                for (int c = row.FirstCellNum; c < row.LastCellNum; c++)
                {
                    var cell = row.GetCell(c);
                    if (cell is null) continue;
                    var target = ws.Cell(r + 1, c + 1);   // NPOI 0-базный, ClosedXML 1-базный
                    var type = cell.CellType == NPOI.SS.UserModel.CellType.Formula
                        ? cell.CachedFormulaResultType : cell.CellType;
                    switch (type)
                    {
                        case NPOI.SS.UserModel.CellType.Numeric: target.Value = cell.NumericCellValue; break;
                        case NPOI.SS.UserModel.CellType.String: target.Value = cell.StringCellValue; break;
                        case NPOI.SS.UserModel.CellType.Boolean: target.Value = cell.BooleanCellValue; break;
                    }
                }
            }
        }
        return wb;
    }

    /// <summary>
    /// Ищет строку заголовков в первых 30 строках листа: строка, где есть колонка «Артикул»
    /// плюс распознаваемые «Наименование»/«Номенклатура» и «Цена». Возвращает карту колонок
    /// или null, если таблица не найдена.
    /// </summary>
    private static ColumnMap? DetectColumns(IXLWorksheet ws)
    {
        foreach (var row in ws.RowsUsed().Take(30))
        {
            var headers = new Dictionary<int, string>();
            foreach (var cell in row.CellsUsed())
            {
                var text = Norm(cell.GetString());
                if (text.Length > 0) headers[cell.Address.ColumnNumber] = text;
            }
            var sku = Find(headers, h => h.StartsWith("артикул"));
            if (sku is null) continue;

            var name = Find(headers, h => h.Contains("наименование") || h.Contains("номенклатура"));
            // Приоритет цене «без НДС»: Price в системе хранится без НДС (НДС добавляет генератор КП).
            var price = Find(headers, h => h.Contains("цена") && h.Contains("без ндс"))
                     ?? Find(headers, h => h == "цена")
                     ?? Find(headers, h => h.Contains("базовая цена"))
                     ?? Find(headers, h => h.Contains("цена"));
            if (name is null || price is null) continue;   // «Артикул» есть, но это не строка заголовков

            return new ColumnMap(row.RowNumber(), sku.Value, name.Value,
                Find(headers, h => h.Contains("производитель")),
                Find(headers, h => h.Contains("категория")),
                price.Value,
                Find(headers, h => h.Contains("срок")),
                Find(headers, h => h.Contains("остаток")));
        }
        return null;
    }

    /// <summary>
    /// Собирает иерархию номенклатуры с листа: ищет строку заголовков с «Артикул» и колонками
    /// «N уровень иерархии», для каждой строки склеивает непустые уровни в путь через « / ».
    /// Листы без таких колонок молча пропускаются.
    /// </summary>
    private static void CollectHierarchy(IXLWorksheet ws, Dictionary<string, string> hierarchy)
    {
        int? skuCol = null;
        List<int>? levelCols = null;
        var headerRow = 0;
        foreach (var row in ws.RowsUsed().Take(30))
        {
            var headers = new Dictionary<int, string>();
            foreach (var cell in row.CellsUsed())
            {
                var text = Norm(cell.GetString());
                if (text.Length > 0) headers[cell.Address.ColumnNumber] = text;
            }
            var sku = Find(headers, h => h.StartsWith("артикул"));
            if (sku is null) continue;
            var levels = headers.Where(h => h.Value.Contains("уровень иерархии"))
                .OrderBy(h => h.Value).Select(h => h.Key).ToList();
            if (levels.Count == 0) continue;
            skuCol = sku; levelCols = levels; headerRow = row.RowNumber();
            break;
        }
        if (skuCol is null || levelCols is null) return;

        foreach (var row in ws.RowsUsed())
        {
            if (row.RowNumber() <= headerRow) continue;
            var sku = row.Cell(skuCol.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(sku)) continue;
            var parts = levelCols.Select(c => row.Cell(c).GetString().Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (parts.Count == 0) continue;
            hierarchy[sku] = string.Join(" / ", parts);
        }
    }

    /// <summary>Нормализация заголовка: нижний регистр, все пробельные последовательности (включая переносы) → один пробел.</summary>
    private static string Norm(string s) =>
        string.Join(' ', s.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int? Find(Dictionary<int, string> headers, Func<string, bool> pred)
    {
        int? best = null;
        foreach (var (col, text) in headers)
            if (pred(text) && (best is null || col < best)) best = col;
        return best;
    }

    /// <summary>Число из ячейки: числовая ячейка — как есть; текстовая — с поддержкой запятой и пробелов-разделителей.</summary>
    private static bool TryReadDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue(out value)) return true;
        var text = cell.GetString().Trim().Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static string? ReadOptionalString(IXLRow row, int? col)
    {
        if (col is null) return null;
        var s = row.Cell(col.Value).GetString().Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static string? MakeCategorySafeForStorage(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        var value = category.Trim();
        if (value.Length <= MaxCategoryLength) return value;

        const string separator = " / ";
        const string omission = " / … / ";
        var parts = value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Сохраняем корень и наиболее конкретные уровни, удаляя середину длинного пути.
        for (var suffixStart = 1; suffixStart < parts.Length; suffixStart++)
        {
            var shortened = $"{parts[0]}{omission}{string.Join(separator, parts.Skip(suffixStart))}";
            if (shortened.Length <= MaxCategoryLength) return shortened;
        }

        const string middleOmission = " … ";
        var tailLength = MaxCategoryLength / 3;
        var headLength = MaxCategoryLength - middleOmission.Length - tailLength;
        return value[..headLength].TrimEnd() + middleOmission + value[^tailLength..].TrimStart();
    }

    private static int? ReadOptionalInt(IXLRow row, int? col)
    {
        if (col is null) return null;
        if (!TryReadDecimal(row.Cell(col.Value), out var v)) return null;
        return (int)v;
    }
}
