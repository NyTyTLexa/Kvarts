using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Ordering;

/// <summary>Статусы жизненного цикла заказа.</summary>
public enum OrderStatus
{
    Draft,       // черновик (только создан из КП)
    Placed,      // размещён у поставщиков
    Confirmed,   // подтверждён
    Shipped,     // отгружен
    Completed,   // завершён
    Cancelled    // отменён
}

/// <summary>
/// Заказ — «застывший» снимок выбранного варианта КП: позиции с зафиксированными ценами
/// и поставщиками на момент формирования (чтобы последующие изменения каталога не влияли на заказ).
/// </summary>
public class Order : AuditableEntity
{
    public string Number { get; set; } = default!;         // человекочитаемый номер
    public Guid? SpecificationId { get; set; }             // из какой спецификации
    public string Title { get; set; } = default!;
    public string? Customer { get; set; }
    public string Strategy { get; set; } = default!;       // стратегия подбора КП
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public decimal TotalCost { get; set; }
    public int MaxLeadTimeDays { get; set; }
    public string? CreatedBy { get; set; }                 // пользователь, оформивший заказ

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

    // ── Поведение ЖЦ (rich domain model) ──────────────────────────────────────────────

    /// <summary>Допустимые переходы статусов заказа.</summary>
    public static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Draft]     = [OrderStatus.Placed, OrderStatus.Cancelled],
            [OrderStatus.Placed]    = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped]   = [OrderStatus.Completed],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    public bool CanTransitionTo(OrderStatus target) => Transitions[Status].Contains(target);

    /// <summary>Перевести заказ в целевой статус. Повтор текущего статуса идемпотентен (true).</summary>
    public bool TryTransitionTo(OrderStatus target)
    {
        if (Status == target) return true;
        if (!CanTransitionTo(target)) return false;
        Status = target;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}
