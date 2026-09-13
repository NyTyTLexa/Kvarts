using Microsoft.EntityFrameworkCore;
using ProcurementSystem.Contracts.Search;
using ProcurementSystem.Domain.Catalog;
using ProcurementSystem.Domain.Search;
using ProcurementSystem.Infrastructure.Persistence;

namespace ProcurementSystem.Infrastructure.Search;

public interface IProductIndexer
{
    /// <summary>Переиндексировать один товар (или удалить из индекса, если его уже нет в БД).</summary>
    Task IndexAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Полная переиндексация каталога (массовое первичное наполнение индекса).</summary>
    Task<int> ReindexAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Собирает денормализованный ProductSearchDocument из товара и его офферов
/// и кладёт в поисковый движок через абстракцию ISearchEngine.
/// </summary>
public class ProductIndexer(AppDbContext db, ISearchEngine search) : IProductIndexer
{
    public const string Index = "products";

    public async Task IndexAsync(Guid productId, CancellationToken ct = default)
    {
        var doc = await Project(db.Products.Where(p => p.Id == productId)).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            await search.DeleteAsync(Index, productId.ToString(), ct);
            return;
        }
        await search.IndexAsync(Index, new[] { doc }, ct);
    }

    public async Task<int> ReindexAllAsync(CancellationToken ct = default)
    {
        // Чистим индекс перед наполнением, чтобы он был точным зеркалом БД и не содержал
        // осиротевших документов от удалённых ранее товаров.
        await search.ClearAsync(Index, ct);

        const int chunk = 1000;
        int total = 0, skip = 0;
        while (true)
        {
            var docs = await Project(db.Products.OrderBy(p => p.Id).Skip(skip).Take(chunk)).ToListAsync(ct);
            if (docs.Count == 0) break;
            await search.IndexAsync(Index, docs, ct);
            total += docs.Count;
            skip += chunk;
        }
        return total;
    }

    private static IQueryable<ProductSearchDocument> Project(IQueryable<Product> q) =>
        q.Select(p => new ProductSearchDocument
        {
            Id = p.Id.ToString(),
            Sku = p.Sku,
            Name = p.Name,
            Manufacturer = p.Manufacturer,
            Category = p.Category,
            MinPrice = p.Offers.Min(o => (decimal?)o.Price) ?? 0m,
            MinLeadTimeDays = p.Offers.Min(o => (int?)o.LeadTimeDays) ?? 0,
            OffersCount = p.Offers.Count()
        });
}
