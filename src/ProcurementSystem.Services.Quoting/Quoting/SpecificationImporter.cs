using ClosedXML.Excel;
using ProcurementSystem.Domain.Matching;
using ProcurementSystem.Domain.Quoting;
using ProcurementSystem.Services.Quoting.Clients;
using ProcurementSystem.Services.Quoting.Persistence;

namespace ProcurementSystem.Services.Quoting.Quoting;

public record SpecImportResult(Guid SpecificationId, int Rows, int Matched, int Unmatched, IReadOnlyList<string> Errors);

public interface ISpecificationImporter
{
    Task<SpecImportResult> ImportAsync(string title, string? customer, Stream excel, CancellationToken ct = default);
}

/// <summary>
/// Импорт спецификации заказчика из Excel.
/// Колонки: A=Артикул, B=Наименование, C=Количество. Первая строка — заголовок.
/// Каждая позиция сопоставляется с каталогом: сперва точно по артикулу,
/// затем ML (TF-IDF + логрегрессия, аналоги по категории).
/// </summary>
public class SpecificationImporter(QuotingDbContext db, IRelevanceMatcher matcher, ICatalogClient catalog) : ISpecificationImporter
{
    public async Task<SpecImportResult> ImportAsync(string title, string? customer, Stream excel, CancellationToken ct = default)
    {
        var spec = new Specification { Title = title, Customer = customer };
        db.Specifications.Add(spec);

        var errors = new List<string>();
        int rows = 0, matched = 0;

        try
        {
            using var wb = new XLWorkbook(excel);
            var ws = wb.Worksheets.First();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                ct.ThrowIfCancellationRequested();
                var sku = row.Cell(1).GetString().Trim();
                var name = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(name)) continue;
                rows++;

                int qty;
                try { qty = row.Cell(3).GetValue<int>(); } catch { qty = 1; }
                if (qty <= 0) qty = 1;

                Guid? productId = null;

                if (!string.IsNullOrWhiteSpace(sku))
                    productId = (await catalog.FindBySkuAsync(sku, ct))?.Id;

                if (productId is null && (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(sku)))
                {
                    try
                    {
                        var hits = await matcher.SuggestAsync(sku, name, 1, ct);
                        if (hits.Count > 0 && hits[0].Probability >= 0.5)
                            productId = hits[0].ProductId;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Строка {row.RowNumber()}: ML-сопоставление недоступно ({ex.Message})");
                    }
                }

                if (productId is not null) matched++;
                spec.Items.Add(new SpecificationItem
                {
                    RawSku = string.IsNullOrWhiteSpace(sku) ? null : sku,
                    RawName = string.IsNullOrWhiteSpace(name) ? sku : name,
                    Quantity = qty,
                    ProductId = productId
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Структура файла не распознана: {ex.Message}");
        }

        await db.SaveChangesAsync(ct);
        return new SpecImportResult(spec.Id, rows, matched, rows - matched, errors);
    }
}
