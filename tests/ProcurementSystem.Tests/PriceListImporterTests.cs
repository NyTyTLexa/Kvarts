using ClosedXML.Excel;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Infrastructure.Import;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Tests;

/// <summary>
/// Гибкий маппинг колонок импорта прайса (ТЗ, раздел рисков: «гибкий маппинг колонок»).
/// Проверяется на двух реальных схемах: наш исторический шаблон (заголовок в строке 1)
/// и формат реального прайса EKF (шапка на 11 строк, свои имена колонок, цены с/без НДС).
/// </summary>
public class PriceListImporterTests
{
    private static async Task<Vendor> SeedVendorAsync(AppDbContext db)
    {
        var v = new Vendor { Name = "Тестовый поставщик", DefaultLeadTimeDays = 7 };
        db.Vendors.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            fill(wb.AddWorksheet("Лист1"));
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Legacy_template_with_header_in_first_row_still_imports()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        using var xlsx = Workbook(ws =>
        {
            string[] head = ["Артикул", "Наименование", "Производитель", "Категория", "Цена", "СрокДней", "Остаток"];
            for (var c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
            ws.Cell(2, 1).Value = "SKU-1"; ws.Cell(2, 2).Value = "Товар 1"; ws.Cell(2, 3).Value = "ACME";
            ws.Cell(2, 4).Value = "Категория А"; ws.Cell(2, 5).Value = 123.45; ws.Cell(2, 6).Value = 14; ws.Cell(2, 7).Value = 5;
        });

        var result = await new ExcelPriceListImporter(db).ImportAsync(vendor.Id, xlsx, "old.xlsx", "test");

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.OffersUpserted);
        var product = Assert.Single(db.Products.Where(p => p.Sku == "SKU-1"));
        Assert.Equal("ACME", product.Manufacturer);
        var offer = Assert.Single(db.PriceListItems);
        Assert.Equal(123.45m, offer.Price);
        Assert.Equal(14, offer.LeadTimeDays);
        Assert.Equal(5, offer.StockQuantity);
    }

    [Fact]
    public async Task Ekf_style_file_with_late_header_and_vat_columns_imports()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        using var xlsx = Workbook(ws =>
        {
            // 11 строк «шапки» как в реальном файле EKF
            ws.Cell(2, 2).Value = "Дата актуальности:";
            ws.Cell(4, 2).Value = "8-800-333-88-15";
            // заголовки в строке 12, имена как в EKF (с переносами строк внутри ячеек)
            ws.Cell(12, 1).Value = "Артикул";
            ws.Cell(12, 2).Value = "Номенклатура";
            ws.Cell(12, 3).Value = "Ссылка на сайт";
            ws.Cell(12, 4).Value = "Сертификат";
            ws.Cell(12, 5).Value = "Заказ";
            ws.Cell(12, 6).Value = "Ед.";
            ws.Cell(12, 7).Value = "Базовая цена,\nс НДС";
            ws.Cell(12, 8).Value = "Базовая цена,\nбез НДС";
            ws.Cell(13, 1).Value = "EXR16-028-10";
            ws.Cell(13, 2).Value = "Розетка 1-местная";
            ws.Cell(13, 7).Value = 379.71;   // с НДС — НЕ должна попасть в систему
            ws.Cell(13, 8).Value = 311.24;   // без НДС — именно она
        });

        var result = await new ExcelPriceListImporter(db).ImportAsync(vendor.Id, xlsx, "ekf.xlsx", "test");

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.OffersUpserted);
        var offer = Assert.Single(db.PriceListItems);
        Assert.Equal(311.24m, offer.Price);                      // выбрана колонка «без НДС»
        Assert.Equal(7, offer.LeadTimeDays);                     // нет колонки срока → срок поставщика
        Assert.Equal(0, offer.StockQuantity);                    // нет колонки остатка → 0
        Assert.Single(db.Products.Where(p => p.Sku == "EXR16-028-10" && p.Name == "Розетка 1-местная"));
    }

    [Fact]
    public async Task Legacy_binary_xls_file_imports_via_npoi()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        // Настоящий бинарный .xls (OLE2), собранный NPOI — как файлы из старых версий Excel.
        using var xls = new MemoryStream();
        var legacy = new NPOI.HSSF.UserModel.HSSFWorkbook();
        var sheet = legacy.CreateSheet("Прайс");
        string[] head = ["Артикул", "Наименование", "Цена"];
        var header = sheet.CreateRow(0);
        for (var c = 0; c < head.Length; c++) header.CreateCell(c).SetCellValue(head[c]);
        var data = sheet.CreateRow(1);
        data.CreateCell(0).SetCellValue("XLS-SKU-1");
        data.CreateCell(1).SetCellValue("Товар из старого Excel");
        data.CreateCell(2).SetCellValue(555.5);
        legacy.Write(xls, leaveOpen: true);
        xls.Position = 0;

        var result = await new ExcelPriceListImporter(db).ImportAsync(vendor.Id, xls, "old-format.xls", "test");

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.OffersUpserted);
        var offer = Assert.Single(db.PriceListItems);
        Assert.Equal(555.5m, offer.Price);
        Assert.Single(db.Products.Where(p => p.Sku == "XLS-SKU-1"));
    }

    [Fact]
    public async Task Hierarchy_from_auxiliary_sheet_fills_product_category()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        // основной лист + вспомогательный с иерархией (как «Промоцена»/«Тарифные зоны» у EKF)
        using var xlsx = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Прайс");
            ws.Cell(1, 1).Value = "Артикул"; ws.Cell(1, 2).Value = "Номенклатура"; ws.Cell(1, 3).Value = "Цена";
            ws.Cell(2, 1).Value = "HIER-1"; ws.Cell(2, 2).Value = "Труба гофрированная"; ws.Cell(2, 3).Value = 100;
            var extra = wb.AddWorksheet("Промоцена");
            extra.Cell(1, 1).Value = "Артикул";
            extra.Cell(1, 2).Value = "1 уровень иерархии";
            extra.Cell(1, 3).Value = "2 уровень иерархии";
            extra.Cell(2, 1).Value = "HIER-1";
            extra.Cell(2, 2).Value = "27 Кабеленесущие системы";
            extra.Cell(2, 3).Value = "27.09 Труба двустенная ПНД";
            wb.SaveAs(xlsx);
        }
        xlsx.Position = 0;

        var result = await new ExcelPriceListImporter(db).ImportAsync(vendor.Id, xlsx, "hier.xlsx", "test");

        Assert.Empty(result.Errors);
        var product = Assert.Single(db.Products.Where(p => p.Sku == "HIER-1"));
        Assert.Equal("27 Кабеленесущие системы / 27.09 Труба двустенная ПНД", product.Category);
    }

    [Fact]
    public async Task File_without_recognizable_header_reports_structure_error()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        using var xlsx = Workbook(ws =>
        {
            ws.Cell(1, 1).Value = "Просто какой-то текст";
            ws.Cell(2, 1).Value = "Без таблицы";
        });

        var result = await new ExcelPriceListImporter(db).ImportAsync(vendor.Id, xlsx, "junk.xlsx", "test");

        Assert.Equal(0, result.OffersUpserted);
        Assert.Contains(result.Errors, e => e.Contains("Структура файла не распознана"));
    }
}
