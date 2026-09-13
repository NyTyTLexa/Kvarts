using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Infrastructure.Persistence;
using ProcurementSystem.Infrastructure.Quoting;

namespace ProcurementSystem.Infrastructure.Commercial;

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
/// переиспользуется <see cref="IQuoteGenerator"/>: CostPrice = quote.TotalCost. Маршрут
/// и формулы наценки/маржи — поведение сущности <see cref="Approval"/> (rich domain model);
/// сервис оркеструет: загрузка → guard-проверки → вызов доменного метода → сохранение.
/// </summary>
public class ApprovalCommandService(AppDbContext db, IQuoteGenerator quotes, IInvoiceCommandService invoices) : IApprovalCommandService
{
    public async Task<(ApprovalDto? Dto, string? Error)> CreateAsync(
        CreateApprovalRequest req, string? createdBy, CancellationToken ct = default)
    {
        if (req.MarkupPercent < 0) return (null, "Наценка не может быть отрицательной");

        var spec = await db.Specifications.AsNoTracking()
            .Where(s => s.Id == req.SpecificationId)
            .Select(s => new { s.Customer })
            .FirstOrDefaultAsync(ct);
        if (spec is null) return (null, "Спецификация не найдена");

        // Себестоимость — из генератора КП (сам генератор не меняем).
        var quote = await quotes.GenerateAsync(req.SpecificationId, req.Strategy, ct: ct);

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
        db.Set<Approval>().Add(a);
        await db.SaveChangesAsync(ct);
        return (a.ToDto(), null);
    }

    public async Task<(bool Found, string? Error)> SetMarginAsync(
        Guid id, SetMarginRequest req, CancellationToken ct = default)
    {
        if (req.MarkupPercent < 0) return (true, "Наценка не может быть отрицательной");
        var a = await db.Set<Approval>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.IsDecided) return (true, "Решение по согласованию уже принято");

        a.ApplyMarkup(req.MarkupPercent);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Found, string? Error)> DecideAsync(
        Guid id, DecisionRequest req, CancellationToken ct = default)
    {
        var a = await db.Set<Approval>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.IsDecided) return (true, "Решение по согласованию уже принято");

        a.Decide(req.Approved, req.Comment);
        await db.SaveChangesAsync(ct);

        // UC-07: финальное согласование КП автоматически создаёт счёт NOC.
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
        var a = await db.Set<Approval>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (false, null);
        if (a.Status != ApprovalStatus.Отклонено)
            return (true, "Повторно отправить можно только отклонённое согласование");

        // Comment намеренно не стирается — предыдущее решение остаётся видно РП до нового Decide.
        a.ReturnForRework();
        await db.SaveChangesAsync(ct);
        return (true, null);
    }
}
