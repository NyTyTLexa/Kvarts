using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Commercial;

/// <summary>
/// Стадии жизненного цикла счёта NOC (ТЗ п.B7). Линейная цепочка — по порядку объявления;
/// «Отменён» — терминальный, допустим из любого не финального состояния.
/// </summary>
public enum InvoiceStatus
{
    Создан,
    Согласован,
    ОжиданиеОплаты,
    ЧастичнаяОплата,
    Оплачено,
    ОжиданиеПоставки,
    ПришёлНаСклад,
    ОтраженоВ1С,
    Отменён
}

/// <summary>
/// Кто меняет стадию счёта (ТЗ 10.11): КБ — «Согласован», бухгалтерия — последующие,
/// системный актор (склад) — без ролевой проверки. admin входит в обе политики.
/// </summary>
public readonly record struct InvoiceStatusActor(bool CanApprove, bool CanPostPayment, bool IsSystem = false)
{
    public static InvoiceStatusActor System { get; } = new(true, true, true);
    public static InvoiceStatusActor Commercial { get; } = new(true, false);
    public static InvoiceStatusActor Accounting { get; } = new(false, true);
    public static InvoiceStatusActor Admin { get; } = new(true, true);
}

/// <summary>
/// Счёт NOC — «застывший» снимок согласованного КП: сумма, контрагент, договор и жизненный
/// цикл по стадиям (ТЗ п.B7). Создаётся только из <see cref="Approval"/> со статусом
/// <see cref="ApprovalStatus.Согласовано"/>. Стадии «ПришёлНаСклад» и «ОтраженоВ1С»
/// выставляет Трек B через POST .../status.
/// </summary>
public class Invoice : AuditableEntity
{
    public string Number { get; set; } = default!;        // человекочитаемый номер NOC-2026-NNN
    public Guid ApprovalId { get; set; }                  // согласование-источник
    public string? Customer { get; set; }
    public string? Contract { get; set; }                 // номер договора

    public decimal CostPrice { get; set; }                // себестоимость (из Approval)
    public decimal MarkupPercent { get; set; }            // наценка, % (из Approval)
    public decimal SellPrice { get; set; }                // сумма счёта = цена клиенту (из Approval)

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Создан;
    public DateTime? DueDateUtc { get; set; }             // срок оплаты
    public string? CreatedBy { get; set; }                // пользователь, создавший счёт

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    // ── Поведение ЖЦ (rich domain model): правила переходов живут в сущности, ─────────
    // ── сервисы приложения лишь оркестрируют загрузку/сохранение.              ─────────

    /// <summary>Линейная цепочка стадий ЖЦ (без терминального «Отменён»).</summary>
    public static readonly InvoiceStatus[] LinearFlow =
    [
        InvoiceStatus.Создан,
        InvoiceStatus.Согласован,
        InvoiceStatus.ОжиданиеОплаты,
        InvoiceStatus.ЧастичнаяОплата,
        InvoiceStatus.Оплачено,
        InvoiceStatus.ОжиданиеПоставки,
        InvoiceStatus.ПришёлНаСклад,
        InvoiceStatus.ОтраженоВ1С
    ];

    /// <summary>
    /// Допустимый переход: только на следующую по порядку стадию ЖЦ (без пропуска стадий —
    /// регрессия Этапа 8), либо «Отменён» из любого не финального состояния.
    /// Финальные: «ОтраженоВ1С» (завершён) и «Отменён».
    /// </summary>
    public bool CanTransitionTo(InvoiceStatus target)
    {
        if (target == InvoiceStatus.Отменён)
            return Status != InvoiceStatus.Отменён && Status != InvoiceStatus.ОтраженоВ1С;

        var ci = Array.IndexOf(LinearFlow, Status);
        var ti = Array.IndexOf(LinearFlow, target);
        return ci >= 0 && ti == ci + 1;   // строго следующая стадия
    }

    /// <summary>Метка автора счёта, созданного при финальном согласовании КП (UC-07).</summary>
    public const string AutoCreatedBy = "Система (авто)";

    /// <summary>
    /// Стадию «Согласован» ставит только КБ (политика approve); все последующие, включая
    /// «Отменён», — бухгалтерия (postPayment). Системный актор склада не ограничивается.
    /// </summary>
    public static bool ActorMaySet(InvoiceStatus target, InvoiceStatusActor actor)
    {
        if (actor.IsSystem) return true;
        if (target == InvoiceStatus.Согласован) return actor.CanApprove;
        return actor.CanPostPayment;
    }

    /// <summary>Перевести счёт в целевую стадию. Повтор текущей стадии идемпотентен (true).</summary>
    public bool TryTransitionTo(InvoiceStatus target)
    {
        if (Status == target) return true;
        if (!CanTransitionTo(target)) return false;
        Status = target;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}
