using ProcurementSystem.Domain.Matching;

namespace ProcurementSystem.Services.Matching.Matching;

public record ItemMatchDto(
    Guid ItemId,
    string? Sku,
    string Name,
    int Quantity,
    Guid? CurrentProductId,
    bool Matched,
    IReadOnlyList<MatchSuggestion> Suggestions);

public record SpecMatchDto(
    Guid SpecificationId,
    string Title,
    int Items,
    int Matched,
    int Unmatched,
    bool ModelReady,
    int CatalogSize,
    IReadOnlyList<ItemMatchDto> ItemsDetail,
    int ElapsedMs,
    int TrainedOn,
    int ExactHits,
    int FuzzyHits,
    int AnalogHits);

public record ApplyMatchItemRequest(Guid ItemId, Guid ProductId);

public record SuggestMatchRequest(string? Sku, string Name, int Take = 5);
