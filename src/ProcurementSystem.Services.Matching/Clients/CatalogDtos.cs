namespace ProcurementSystem.Services.Matching.Clients;

/// <summary>Снимок товара каталога. Нужные для TF-IDF поля лежат здесь, не через Include.</summary>
public sealed record CatalogProductSnapshot(
    Guid Id,
    string Sku,
    string Name,
    string? Manufacturer,
    string? Category);
