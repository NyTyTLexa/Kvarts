using ProcurementSystem.Domain.Common;

namespace ProcurementSystem.Domain.Projects;

/// <summary>
/// Карточка проекта (ТЗ п.5.2: «Проект — ID, наименование, РП, дата создания, статус»).
/// Лёгкая «обёртка» поверх спецификации: сама бизнес-логика КП/согласования/счёта живёт
/// в Quoting/Commercial, здесь — только группировка для дашборда. Привязка к спецификации
/// необязательна и хранится простым Guid-FK без навигационного свойства на стороне
/// Specification — чтобы не трогать модуль Quoting.
/// </summary>
public class Project : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Rp { get; set; }               // руководитель проекта (владелец)
    public DateTime? DueDateUtc { get; set; }
    public Guid? SpecificationId { get; set; }     // необязательная привязка к спецификации/КП
}
