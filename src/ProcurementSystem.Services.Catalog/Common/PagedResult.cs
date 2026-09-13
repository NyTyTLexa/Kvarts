namespace ProcurementSystem.Services.Catalog.Common;

/// <summary>Страница результатов с общим количеством — общий контракт для списков.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
