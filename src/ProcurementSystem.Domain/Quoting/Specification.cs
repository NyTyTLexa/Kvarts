using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Quoting;

/// <summary>Спецификация (заявка заказчика): перечень требуемых позиций с количеством.</summary>
public class Specification : AuditableEntity
{
    public string Title { get; set; } = default!;          // название заявки / проекта
    public string? Customer { get; set; }                  // заказчик

    public ICollection<SpecificationItem> Items { get; set; } = new List<SpecificationItem>();
}
