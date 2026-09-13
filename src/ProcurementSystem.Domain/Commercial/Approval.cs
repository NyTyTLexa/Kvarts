using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Commercial;

/// <summary>Стадии согласования КП в коммерческом блоке (ТЗ п.B6).</summary>
public enum ApprovalStatus
{
    НаСогласованииРП,     // создано, на согласовании у руководителя проекта
    ВКоммерческомБлоке,   // наценка задана, передано в коммерческий блок
    Согласовано,          // согласовано
    Отклонено             // отклонено
}

/// <summary>
/// Согласование КП в коммерческом блоке: себестоимость из генератора КП
/// (<see cref="ProcurementSystem.Infrastructure.Quoting.IQuoteGenerator"/> → quote.TotalCost),
/// наценка РП/КБ и итоговая цена клиенту с расчётом маржинальности (ТЗ п.B6).
/// Маршрут согласования отражается полем <see cref="Status"/>.
/// </summary>
public class Approval : AuditableEntity
{
    public Guid SpecificationId { get; set; }             // спецификация-источник КП
    public string Strategy { get; set; } = default!;      // стратегия КП (имя QuoteStrategy)
    public string Title { get; set; } = default!;         // название спецификации/заявки
    public string? Customer { get; set; }                 // заказчик (из спецификации)

    public decimal CostPrice { get; set; }                // себестоимость (TotalCost из КП)
    public decimal MarkupPercent { get; set; }            // наценка, %
    public decimal SellPrice { get; set; }                // цена клиенту = CostPrice*(1+Markup/100)
    public decimal MarginPercent { get; set; }            // маржа, % = (Sell-Cost)/Sell*100

    public ApprovalStatus Status { get; set; } = ApprovalStatus.НаСогласованииРП;
    public string? CreatedBy { get; set; }                // пользователь, создавший согласование
    public DateTime? DecidedAtUtc { get; set; }           // когда принято решение
    public string? Comment { get; set; }                  // комментарий при решении

    // ── Поведение маршрута согласования (rich domain model) ───────────────────────────

    /// <summary>Решение уже принято (согласовано или отклонено) — маршрут завершён.</summary>
    public bool IsDecided => Status is ApprovalStatus.Согласовано or ApprovalStatus.Отклонено;

    /// <summary>Цена клиенту по формуле ТЗ: себестоимость × (1 + наценка/100).</summary>
    public static decimal CalcSellPrice(decimal cost, decimal markupPercent) =>
        cost * (1m + markupPercent / 100m);

    /// <summary>Маржинальность по формуле ТЗ: (цена − себестоимость) / цена × 100.</summary>
    public static decimal CalcMarginPercent(decimal cost, decimal markupPercent)
    {
        var sell = CalcSellPrice(cost, markupPercent);
        return sell > 0 ? (sell - cost) / sell * 100m : 0m;
    }

    /// <summary>Задать наценку: пересчитать цену клиенту и маржу, передать в коммерческий блок.</summary>
    public void ApplyMarkup(decimal markupPercent)
    {
        MarkupPercent = markupPercent;
        SellPrice = CalcSellPrice(CostPrice, markupPercent);
        MarginPercent = CalcMarginPercent(CostPrice, markupPercent);
        Status = ApprovalStatus.ВКоммерческомБлоке;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Зафиксировать решение коммерческого блока.</summary>
    public void Decide(bool approved, string? comment)
    {
        Status = approved ? ApprovalStatus.Согласовано : ApprovalStatus.Отклонено;
        DecidedAtUtc = DateTime.UtcNow;
        Comment = comment;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Вернуть отклонённое на доработку (ТЗ п.5.6): шаг назад, комментарий сохраняется.</summary>
    public void ReturnForRework()
    {
        Status = ApprovalStatus.НаСогласованииРП;
        DecidedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
