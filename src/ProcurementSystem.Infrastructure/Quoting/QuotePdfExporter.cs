using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProcurementSystem.Infrastructure.Quoting;

public interface IQuotePdfExporter
{
    byte[] Export(Quote quote);
}

/// <summary>
/// Формирует готовое КП в виде PDF (ТЗ: экспорт «желательно PDF», подтверждено заказчиком).
/// Содержимое зеркалит Excel-экспорт (<see cref="QuoteExcelExporter"/>): шапка, таблица позиций,
/// итоги без/с НДС, срок и число поставщиков; несопоставленные позиции — красным.
/// </summary>
public class QuotePdfExporter : IQuotePdfExporter
{
    static QuotePdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Export(Quote quote)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("Коммерческое предложение").FontSize(15).Bold();
                    col.Item().Text($"Спецификация: {quote.SpecificationTitle}").FontSize(10);
                    col.Item().PaddingBottom(6).Text($"Стратегия подбора: {StrategyRu(quote.Strategy)}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22);    // №
                            c.RelativeColumn(1.4f);  // Артикул
                            c.RelativeColumn(3.2f);  // Наименование
                            c.ConstantColumn(38);    // Кол-во
                            c.RelativeColumn(1.6f);  // Поставщик
                            c.RelativeColumn(1.2f);  // Производитель
                            c.RelativeColumn(1.1f);  // Цена
                            c.ConstantColumn(46);    // Скидка
                            c.RelativeColumn(1.1f);  // Цена со скидкой
                            c.RelativeColumn(1.2f);  // Сумма
                            c.ConstantColumn(40);    // Срок
                            c.RelativeColumn(1.6f);  // Причина выбора
                        });

                        string[] headers = ["№", "Артикул", "Наименование", "Кол-во", "Поставщик", "Производитель",
                            "Цена, ₽", "Скидка", "Со скидкой, ₽", "Сумма, ₽", "Срок, дн", "Причина выбора"];
                        table.Header(h =>
                        {
                            foreach (var text in headers)
                                h.Cell().Background(Colors.Grey.Lighten3).BorderBottom(0.8f).BorderColor(Colors.Grey.Darken1)
                                    .PaddingVertical(3).PaddingHorizontal(4).Text(text).Bold();
                        });

                        var n = 1;
                        foreach (var line in quote.Lines)
                        {
                            var color = line.Matched ? Colors.Black : Colors.Red.Darken1;
                            IContainer Cell() => table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2)
                                .PaddingVertical(2.5f).PaddingHorizontal(4);
                            void Text(string s) => Cell().Text(s).FontColor(color);
                            void Num(string s) => Cell().AlignRight().Text(s).FontColor(color);

                            Text((n++).ToString());
                            Text(line.Sku ?? "");
                            Text(line.Name);
                            Num(line.Quantity.ToString());
                            Text(line.Matched ? line.VendorName ?? "" : "— нет предложений —");
                            Text(line.Manufacturer ?? "");
                            Num(line.Matched ? line.UnitPrice.ToString("N2") : "—");
                            Num(line.Matched && line.DiscountPercent > 0 ? $"−{line.DiscountPercent:0.##}%" : "—");
                            Num(line.Matched ? line.UnitPriceDiscounted.ToString("N2") : "—");
                            Num(line.Matched ? line.LineTotal.ToString("N2") : "—");
                            Num(line.Matched ? line.LeadTimeDays.ToString() : "—");
                            Text(line.Matched ? line.SelectionReason ?? "" : "");
                        }
                    });

                    var vatAmount = quote.TotalCostWithVat - quote.TotalCost;
                    col.Item().PaddingTop(10).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"ИТОГО без НДС: {quote.TotalCost:N2} ₽").FontSize(10).Bold();
                        totals.Item().Text($"НДС {quote.VatRate:0.##}%: {vatAmount:N2} ₽").FontSize(10);
                        totals.Item().Text($"ИТОГО с НДС: {quote.TotalCostWithVat:N2} ₽").FontSize(11).Bold();
                        totals.Item().PaddingTop(4).Text($"Срок поставки (макс.): {quote.MaxLeadTimeDays} дн · Поставщиков: {quote.VendorsUsed}").FontColor(Colors.Grey.Darken1);
                        if (quote.UnmatchedPositions > 0)
                            totals.Item().Text($"Позиций без предложений: {quote.UnmatchedPositions}").FontColor(Colors.Red.Darken1);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Сформировано {DateTime.Now:dd.MM.yyyy HH:mm} · стр. ").FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontColor(Colors.Grey.Darken1);
                    t.Span(" из ").FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return doc.GeneratePdf();
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
