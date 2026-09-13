namespace ProcurementSystem.Domain.Matching;

/// <summary>Как позиция спецификации оказалась связана с товаром каталога.</summary>
public enum MatchKind
{
    None,
    ExactSku,     // точный артикул (ТЗ п.5.3 — приоритет)
    FuzzyName,    // TF-IDF / логистическая модель по наименованию
    Analog        // та же нижняя категория, нет точного товара
}

public record MatchSuggestion(
    Guid ProductId,
    string Sku,
    string Name,
    string? Manufacturer,
    string? Category,
    MatchKind Kind,
    double Probability,
    double NameCosine,
    string Reason);

/// <summary>
/// ML-сопоставление перечня с каталогом и выбор актуального оффера (ТЗ п.5.3, 5.5).
/// Кандидаты собираются из точного артикула, векторного поиска по имени и аналогов;
/// вероятность — логистическая регрессия на признаках; оффер ранжируется по свежести цены,
/// наличию, цене со скидкой и сроку.
/// </summary>
public interface IRelevanceMatcher
{
    int TrainedOn { get; }

    Task EnsureReadyAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MatchSuggestion>> SuggestAsync(
        string? sku, string name, int take = 5, CancellationToken ct = default);
}
