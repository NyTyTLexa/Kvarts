using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Quoting;

/// <summary>
/// Позиция спецификации. Может быть сопоставлена с товаром каталога (ProductId),
/// либо остаться несопоставленной — тогда хранится исходный текст из заявки.
/// </summary>
public class SpecificationItem : Entity
{
    public Guid SpecificationId { get; set; }
    public Specification Specification { get; set; } = default!;

    public Guid? ProductId { get; set; }                   // сопоставленный товар (null = не найден)
    public Product? Product { get; set; }

    public string? RawSku { get; set; }                    // артикул как пришёл в заявке
    public string RawName { get; set; } = default!;        // наименование как пришло в заявке
    public int Quantity { get; set; } = 1;
}
