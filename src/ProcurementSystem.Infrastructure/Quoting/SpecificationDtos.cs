namespace ProcurementSystem.Infrastructure.Quoting;

public record SpecificationDto(Guid Id, string Title, string? Customer, int ItemsCount, int MatchedCount, DateTime CreatedAtUtc);
public record SpecificationItemDto(Guid Id, Guid? ProductId, string? Sku, string Name, int Quantity, bool Matched);
public record SpecificationDetailDto(Guid Id, string Title, string? Customer, DateTime CreatedAtUtc, IReadOnlyList<SpecificationItemDto> Items);

public record CreateSpecificationRequest(string Title, string? Customer);
public record AddSpecificationItemRequest(Guid? ProductId, string? Sku, string Name, int Quantity);

/// <summary>Ручной выбор поставщика для позиции спецификации (ТЗ п.5.5).</summary>
public record SetVendorOverrideRequest(Guid VendorId);
