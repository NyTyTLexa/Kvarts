using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Services.Commercial.Neighbors;
using ProcurementSystem.Services.Commercial.Persistence;

namespace ProcurementSystem.Services.Commercial.Commercial;

/// <summary>Команды согласования КП (CQRS: command-сторона — мутации агрегата Approval).</summary>
public interface IApprovalCommandService
{
    Task<(ApprovalDto? Dto, string? Error)> CreateAsync(CreateApprovalRequest req, string? createdBy, CancellationToken ct = default);
    Task<(bool Found, string? Error)> SetMarginAsync(Guid id, SetMarginRequest req, CancellationToken ct = default);
    Task<(bool Found, string? Error)> DecideAsync(Guid id, DecisionRequest req, CancellationToken ct = default);

    /// <summary>Вернуть отклонённое согласование на доработку (ТЗ п.5.6: «при отклонении процесс
    /// возвращается на шаг назад с сохранением комментария»).</summary>
    Task<(bool Found, string? Error)> ResubmitAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Согласование КП в коммерческом блоке (ТЗ п.B6). Себестоимость здесь не считается —
/// берётся у соседа quoting: CostPrice = quote.TotalCost. Маршрут и формулы наценки/маржи —
/// поведение сущности <see cref="Approval"/>; сервис оркестрирует загрузку и сохранение.
/// </summary>
public class ApprovalCommandService(CommercialDbContext db, IQuotingClient quoting, IInvoiceCommandService invoices) : IApprovalCommandService
{
    public async Task<(ApprovalDto? Dto, string? Error)> CreateAsync(
        CreateApprovalRequest req, string? createdBy, CancellationToken ct = default)
    {
        if (req.MarkupPercent < 0) return (null, "Наценка не может быть отрицательной");

        NeighborSpecification? spec;
        NeighborQuote? quote;
        try
        {
            spec = await quoting.GetSpecificationAsync(req.SpecificationId, ct);
            if (spec is null) return (null, "Спецификация не найдена");
            quote = await quoting.GenerateQuoteAsync(req.SpecificationId, req.Strategy, ct);
        }
        catch (HttpRequestException ex)
        {
            return (null, ex.Message);
        }
        if (quote is null) return (null, "Не удалось сгенерировать КП по спецификации");

        var a = new Approval
        {
            SpecificationId = req.SpecificationId,
            Strategy = req.Strategy.ToString(),
            Title = quote.SpecificationTitle,
            Customer = spec.Customer,
            CostPrice = quote.TotalCost,
            MarkupPercent = req.MarkupPercent,
            SellPrice = Approval.CalcSellPrice(quote.TotalCost, req.MarkupPercent),
            MarginPercent = Approval.CalcMarginPercent(quote.TotalCost, req.MarkupPercent),
            Status = ApprovalStatus.НаСогласованииРП,
            CreatedBy = createdBy
        };
        db.Approvals.Add(a);
        await db.SaveChangesAsync(ct);
        return (a.ToDto(), null);
    }

    public async Task<(bool Found, string? Error)> SetMarginAsync(
        Guid id, SetMarginRequest req, CancellationToken ct = default)
    {
        if (req.MarkupPercent < 0) return (true, "Наценка не может быть отрицательной");
        var a = await db.Approvals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.IsDecided) return (true, "Решение по согласованию уже принято");

        a.ApplyMarkup(req.MarkupPercent);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Found, string? Error)> DecideAsync(
        Guid id, DecisionRequest req, CancellationToken ct = default)
    {
        var a = await db.Approvals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.IsDecided) return (true, "Решение по согласованию уже принято");
        if (a.Status != ApprovalStatus.ВКоммерческомБлоке)
            return (true, $"Решение принимает коммерческий блок только из статуса «В коммерческом блоке». Текущий статус: «{a.Status}».");

        a.Decide(req.Approved, req.Comment);
        await db.SaveChangesAsync(ct);

        if (req.Approved)
        {
            var (_, error, _) = await invoices.CreateAsync(
                new CreateInvoiceRequest(a.Id, null, null), Invoice.AutoCreatedBy, ct);
            if (error is not null) return (true, error);
        }

        return (true, null);
    }

    public async Task<(bool Found, string? Error)> ResubmitAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.Approvals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.Status != ApprovalStatus.Отклонено)
            return (true, "Повторно отправить можно только отклонённое согласование");

        // Comment намеренно не стирается — предыдущее решение остаётся видно РП до нового Decide.
        a.ReturnForRework();
        await db.SaveChangesAsync(ct);
        return (true, null);
    }
}
