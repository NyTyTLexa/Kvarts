using System.Text.Json.Serialization;

namespace ProcurementSystem.Contracts.Search;

/// <summary>
/// Денормализованный плоский документ для поискового индекса.
/// Имена полей зафиксированы атрибутами — так схема индекса не зависит от настроек сериализации
/// и одинаково ложится и в Meilisearch, и в Elasticsearch.
/// </summary>
public class ProductSearchDocument
{
    [JsonPropertyName("id")] public string Id { get; set; } = default!;   // = ProductId, первичный ключ
    [JsonPropertyName("sku")] public string Sku { get; set; } = default!;
    [JsonPropertyName("name")] public string Name { get; set; } = default!;
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("minPrice")] public decimal MinPrice { get; set; }
    [JsonPropertyName("minLeadTimeDays")] public int MinLeadTimeDays { get; set; }
    [JsonPropertyName("offersCount")] public int OffersCount { get; set; }
}
