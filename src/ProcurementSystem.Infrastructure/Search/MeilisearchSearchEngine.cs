using Meilisearch;
using ProcurementSystem.Domain.Search;

namespace ProcurementSystem.Infrastructure.Search;

/// <summary>
/// Адаптер ISearchEngine поверх Meilisearch.
/// Переход на Elasticsearch = новый класс ElasticsearchSearchEngine с тем же интерфейсом.
/// </summary>
public class MeilisearchSearchEngine(MeilisearchClient client) : ISearchEngine
{
    public Task IndexAsync<T>(string index, IEnumerable<T> documents, CancellationToken ct = default)
        where T : class
        => client.Index(index).AddDocumentsAsync(documents, primaryKey: "id", cancellationToken: ct);

    public Task DeleteAsync(string index, string id, CancellationToken ct = default)
        => client.Index(index).DeleteOneDocumentAsync(id, cancellationToken: ct);

    public Task ClearAsync(string index, CancellationToken ct = default)
        => client.Index(index).DeleteAllDocumentsAsync(cancellationToken: ct);

    public async Task<IReadOnlyList<T>> SearchAsync<T>(string index, string query, int limit = 20, CancellationToken ct = default)
        where T : class
    {
        var result = await client.Index(index)
            .SearchAsync<T>(query, new SearchQuery { Limit = limit }, cancellationToken: ct);
        return result.Hits.ToList();
    }
}
