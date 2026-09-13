using ClosedXML.Excel;

namespace ProcurementSystem.Infrastructure.Quoting;

public interface IQuoteExcelExporter
{
    byte[] Export(Quote quote);
}

/// <summary>Формирует готовое КП в виде Excel-файла (xlsx) через ClosedXML.</summary>
public class QuoteExcelExporter : IQuoteExcelExporter
{
    public byte[] Export(Quote quote)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("КП");

        ws.Cell(1, 1).Value = "Коммерческое предложение";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Спецификация: {quote.SpecificationTitle}";
        ws.Cell(3, 1).Value = $"Стратегия подбора: {StrategyRu(quote.Strategy)}";

        string[] headers = ["№", "Артикул", "Наименование", "Кол-во", "Поставщик", "Производитель",
            "Цена за ед., ₽", "Скидка, %", "Цена со скидкой, ₽", "Сумма, ₽", "Срок, дней", "Наличие", "Причина выбора"];
        const int hr = 5;
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(hr, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        int r = hr + 1, n = 1;
        foreach (var line in quote.Lines)
        {
            ws.Cell(r, 1).Value = n++;
            ws.Cell(r, 2).Value = line.Sku ?? "";
            ws.Cell(r, 3).Value = line.Name;
            ws.Cell(r, 4).Value = line.Quantity;
            ws.Cell(r, 5).Value = line.Matched ? line.VendorName : "— нет предложений —";
            ws.Cell(r, 6).Value = line.UnitPrice;
            ws.Cell(r, 7).Value = line.DiscountPercent;
            ws.Cell(r, 8).Value = line.UnitPriceDiscounted;
            ws.Cell(r, 9).Value = line.LineTotal;
            ws.Cell(r, 10).Value = line.Matched ? line.LeadTimeDays : 0;
            ws.Cell(r, 11).Value = line.StockQuantity;
            ws.Cell(r, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 7).Style.NumberFormat.Format = "0.##";
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 12).Value = line.Manufacturer ?? "";
            ws.Cell(r, 13).Value = line.Matched ? (line.SelectionReason ?? "") : "";
            if (!line.Matched)
                ws.Range(r, 1, r, headers.Length).Style.Font.FontColor = XLColor.Red;
            r++;
        }

        r++;
        // Итоги: без НДС, сумма НДС, с НДС (сумма — в колонке «Сумма, ₽»).
        var vatAmount = quote.TotalCostWithVat - quote.TotalCost;
        void Total(int row, string label, decimal value)
        {
            ws.Cell(row, 5).Value = label;
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = value;
            ws.Cell(row, 9).Style.Font.Bold = true;
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
        }
        Total(r, "ИТОГО без НДС, ₽:", quote.TotalCost);
        Total(r + 1, $"НДС {quote.VatRate:0.##}%, ₽:", vatAmount);
        Total(r + 2, "ИТОГО с НДС, ₽:", quote.TotalCostWithVat);

        ws.Cell(r + 3, 5).Value = "Срок поставки (макс.), дней:";
        ws.Cell(r + 3, 10).Value = quote.MaxLeadTimeDays;
        ws.Cell(r + 4, 5).Value = "Поставщиков задействовано:";
        ws.Cell(r + 4, 10).Value = quote.VendorsUsed;
        if (quote.UnmatchedPositions > 0)
        {
            ws.Cell(r + 5, 5).Value = "Позиций без предложений:";
            ws.Cell(r + 5, 10).Value = quote.UnmatchedPositions;
            ws.Cell(r + 5, 5).Style.Font.FontColor = XLColor.Red;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string StrategyRu(QuoteStrategy s) => s switch
    {
        QuoteStrategy.MinCost => "минимальная стоимость",
        QuoteStrategy.MinLeadTime => "минимальный срок поставки",
        QuoteStrategy.Balanced => "сбалансированная (цена/срок)",
        QuoteStrategy.MlRelevance => "ML-актуальность (свежесть цены, наличие, сходство)",
        _ => s.ToString()
    };
}
