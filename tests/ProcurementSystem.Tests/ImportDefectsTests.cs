using ClosedXML.Excel;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Infrastructure.Import;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Tests;

public class ImportDefectsTests
{
    private static async Task<Vendor> SeedVendorAsync(AppDbContext db)
    {
        var vendor = new Vendor { Name = "Тестовый поставщик", DefaultLeadTimeDays = 7 };
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();
        return vendor;
    }

    [Fact]
    public async Task Xlsx_with_products_on_second_sheet_imports_rows()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        using var xlsx = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            // Титульные листы поставщиков не должны мешать поиску фактической таблицы товаров.
            var cover = workbook.AddWorksheet("Титульный лист");
            cover.Cell(1, 1).Value = "Прайс-лист";
            cover.Cell(2, 2).Value = "Действует с сегодняшнего дня";

            var products = workbook.AddWorksheet("Товары");
            FillPriceList(products, "XLSX");
            workbook.SaveAs(xlsx);
        }
        xlsx.Position = 0;

        var result = await new ExcelPriceListImporter(db)
            .ImportAsync(vendor.Id, xlsx, "second-sheet.xlsx", "test");

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.OffersUpserted);
        Assert.Equal(2, db.Products.Count());
    }

    [Fact]
    public async Task Legacy_xls_with_products_on_second_sheet_imports_rows()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        using var xls = new MemoryStream();
        using (var workbook = new NPOI.HSSF.UserModel.HSSFWorkbook())
        {
            // В старых прайсах полезный лист также часто идёт после обложки.
            var cover = workbook.CreateSheet("Титульный лист");
            cover.CreateRow(0).CreateCell(0).SetCellValue("Прайс-лист");
            cover.CreateRow(1).CreateCell(1).SetCellValue("Действует с сегодняшнего дня");

            var products = workbook.CreateSheet("Товары");
            var header = products.CreateRow(0);
            header.CreateCell(0).SetCellValue("Артикул");
            header.CreateCell(1).SetCellValue("Наименование");
            header.CreateCell(2).SetCellValue("Цена");

            var first = products.CreateRow(1);
            first.CreateCell(0).SetCellValue("XLS-1");
            first.CreateCell(1).SetCellValue("Первый товар");
            first.CreateCell(2).SetCellValue(100.50);

            var second = products.CreateRow(2);
            second.CreateCell(0).SetCellValue("XLS-2");
            second.CreateCell(1).SetCellValue("Второй товар");
            second.CreateCell(2).SetCellValue(200.75);

            workbook.Write(xls, leaveOpen: true);
        }
        xls.Position = 0;

        var result = await new ExcelPriceListImporter(db)
            .ImportAsync(vendor.Id, xls, "second-sheet.xls", "test");

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.OffersUpserted);
        Assert.Equal(2, db.Products.Count());
    }

    [Fact]
    public async Task Hierarchy_path_longer_than_category_column_is_made_safe_for_storage()
    {
        using var db = TestDb.Create();
        var vendor = await SeedVendorAsync(db);
        using var xlsx = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var products = workbook.AddWorksheet("Прайс");
            products.Cell(1, 1).Value = "Артикул";
            products.Cell(1, 2).Value = "Наименование";
            products.Cell(1, 3).Value = "Цена";
            products.Cell(2, 1).Value = "LONG-HIERARCHY-1";
            products.Cell(2, 2).Value = "Товар с длинной категорией";
            products.Cell(2, 3).Value = 999.99;

            var hierarchy = workbook.AddWorksheet("Иерархия");
            hierarchy.Cell(1, 1).Value = "Артикул";
            hierarchy.Cell(2, 1).Value = "LONG-HIERARCHY-1";
            for (var level = 1; level <= 4; level++)
            {
                hierarchy.Cell(1, level + 1).Value = $"{level} уровень иерархии";
                hierarchy.Cell(2, level + 1).Value = $"Уровень {level} {new string((char)('а' + level - 1), 75)}";
            }

            workbook.SaveAs(xlsx);
        }
        xlsx.Position = 0;

        var result = await new ExcelPriceListImporter(db)
            .ImportAsync(vendor.Id, xlsx, "long-hierarchy.xlsx", "test");

        Assert.Empty(result.Errors);
        var product = Assert.Single(db.Products.Where(p => p.Sku == "LONG-HIERARCHY-1"));
        Assert.NotNull(product.Category);

        // InMemory не проверяет varchar(300), поэтому ловим несовместимое с реальной БД значение напрямую.
        Assert.True(
            product.Category.Length <= 300,
            $"Категория должна помещаться в varchar(300), фактическая длина: {product.Category.Length}.");
    }

    [Fact(Skip = "ClosedXML всегда присваивает создаваемой картинке непустое имя; воспроизвести дефект полностью в памяти без подделки OOXML не удалось.")]
    public void Xlsx_with_unnamed_picture_imports_rows()
    {
        // Пропуск сохраняет реальный сценарий до появления честного способа собрать такую книгу.
    }

    private static void FillPriceList(IXLWorksheet worksheet, string skuPrefix)
    {
        worksheet.Cell(1, 1).Value = "Артикул";
        worksheet.Cell(1, 2).Value = "Наименование";
        worksheet.Cell(1, 3).Value = "Цена";

        worksheet.Cell(2, 1).Value = $"{skuPrefix}-1";
        worksheet.Cell(2, 2).Value = "Первый товар";
        worksheet.Cell(2, 3).Value = 100.50;

        worksheet.Cell(3, 1).Value = $"{skuPrefix}-2";
        worksheet.Cell(3, 2).Value = "Второй товар";
        worksheet.Cell(3, 3).Value = 200.75;
    }
}
