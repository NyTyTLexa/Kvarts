namespace ProcurementSystem.Domain.Search;

/// <summary>
/// Абстракция поискового движка. Сегодня за ней Meilisearch.
/// Чтобы перейти на Elasticsearch — достаточно написать новый адаптер с этим же
/// интерфейсом и сменить одну строку регистрации в DI. Бизнес-логика не меняется.
/// </summary>
public interface ISearchEngine
{
    Task IndexAsync<T>(string index, IEnumerable<T> documents, CancellationToken ct = default) where T : class;
    Task DeleteAsync(string index, string id, CancellationToken ct = default);

    /// <summary>Полностью очистить индекс (удалить все документы). Нужно, чтобы индекс не хранил
    /// осиротевшие документы после сброса/полной переиндексации каталога.</summary>
    Task ClearAsync(string index, CancellationToken ct = default);
    Task<IReadOnlyList<T>> SearchAsync<T>(string index, string query, int limit = 20, CancellationToken ct = default) where T : class;
}
