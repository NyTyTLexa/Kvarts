namespace ProcurementSystem.Services.Commercial.Commercial;

/// <summary>
/// Стратегия подбора офферов. Значения и JSON-имена совпадают с монолитным
/// <c>ProcurementSystem.Infrastructure.Quoting.QuoteStrategy</c> — контракт POST /api/approvals
/// менять нельзя. Сам генератор КП в этот сервис не переносится.
/// </summary>
public enum QuoteStrategy
{
    MinCost,
    MinLeadTime,
    Balanced,
    MlRelevance
}
