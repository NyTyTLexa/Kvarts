namespace ProcurementSystem.Services.Ordering;

/// <summary>Стратегия подбора офферов под спецификацию.</summary>
public enum QuoteStrategy
{
    MinCost,       // минимальная стоимость
    MinLeadTime,   // минимальный срок поставки
    Balanced,      // взвешенный баланс цена/срок
    MlRelevance    // ML: свежая цена + наличие + сходство (ТЗ п.5.3/5.5)
}
