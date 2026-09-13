using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Events;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Services.Commercial.Persistence;

namespace ProcurementSystem.Services.Commercial.Commercial;

/// <summary>
/// Сшивка склад → счёт: по спецификации заказа находит счёт NOC и продвигает
/// «ОжиданиеПоставки» → «ПришёлНаСклад» → «ОтраженоВ1С» системным актором.
/// Без NATS — его вызывает фоновый консьюмер. Повтор события идемпотентен.
/// </summary>
public class GoodsReceiptCompletedHandler(
    CommercialDbContext db,
    IInvoiceCommandService invoices,
    ILogger<GoodsReceiptCompletedHandler> logger)
{
    public async Task HandleAsync(GoodsReceiptCompleted evt, CancellationToken ct = default)
    {
        if (evt.SpecificationId is not Guid specId || specId == Guid.Empty)
        {
            logger.LogInformation(
                "GoodsReceiptCompleted {ReceiptId}: нет SpecificationId — счёт не ищем",
                evt.ReceiptId);
            return;
        }

        var waitingId = await FindInvoiceIdAsync(specId, InvoiceStatus.ОжиданиеПоставки, ct);
        if (waitingId is Guid iid)
        {
            await AdvanceAsync(iid, InvoiceStatus.ПришёлНаСклад, ct);
            await AdvanceAsync(iid, InvoiceStatus.ОтраженоВ1С, ct);
            logger.LogInformation(
                "Приёмка {ReceiptId}: счёт {InvoiceId} продвинут до «ОтраженоВ1С»",
                evt.ReceiptId, iid);
            return;
        }

        // Повтор после частичного успеха (упали между двумя переходами) или полная доставка.
        var arrivedId = await FindInvoiceIdAsync(specId, InvoiceStatus.ПришёлНаСклад, ct);
        if (arrivedId is Guid arrived)
        {
            await AdvanceAsync(arrived, InvoiceStatus.ОтраженоВ1С, ct);
            logger.LogInformation(
                "Приёмка {ReceiptId}: счёт {InvoiceId} доведён до «ОтраженоВ1С»",
                evt.ReceiptId, arrived);
            return;
        }

        var posted = await FindInvoiceIdAsync(specId, InvoiceStatus.ОтраженоВ1С, ct);
        if (posted is not null)
        {
            logger.LogInformation(
                "Приёмка {ReceiptId}: счёт {InvoiceId} уже в «ОтраженоВ1С» — повтор пропускаем",
                evt.ReceiptId, posted);
            return;
        }

        logger.LogInformation(
            "Приёмка {ReceiptId}: связанного счёта по спецификации {SpecId} нет",
            evt.ReceiptId, specId);
    }

    /// <summary>
    /// Как в монолите: счёт ↔ согласование по ApprovalId, согласование по SpecificationId,
    /// из подходящих — самый свежий.
    /// </summary>
    private async Task<Guid?> FindInvoiceIdAsync(Guid specId, InvoiceStatus status, CancellationToken ct) =>
        await db.Invoices.AsNoTracking()
            .Where(i => i.Status == status)
            .Join(db.Approvals.AsNoTracking().Where(a => a.SpecificationId == specId),
                i => i.ApprovalId, a => a.Id, (i, a) => i)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);

    private async Task AdvanceAsync(Guid invoiceId, InvoiceStatus target, CancellationToken ct)
    {
        var result = await invoices.SetStatusAsync(
            invoiceId, new SetInvoiceStatusRequest(target), InvoiceStatusActor.System, ct);
        if (!result.Found)
            throw new InvalidOperationException($"Счёт {invoiceId} не найден при обработке приёмки");
        if (!result.Ok)
            throw new InvalidOperationException(
                result.Error ?? $"Не удалось перевести счёт {invoiceId} в «{target}»");
    }
}
