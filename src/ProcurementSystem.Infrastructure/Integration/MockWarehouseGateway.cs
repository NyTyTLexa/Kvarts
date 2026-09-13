using Microsoft.Extensions.Logging;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Infrastructure.Observability;

namespace ProcurementSystem.Infrastructure.Integration;

/// <summary>
/// Мок-адаптер внешней складской системы (WMS), Этап 8. Имитирует регистрацию приёмки:
/// возвращает «внешнюю» ссылку и логирует. Задел под реальную интеграцию.
/// </summary>
public class MockWarehouseGateway(ILogger<MockWarehouseGateway> log) : IWarehouseGateway
{
    public string SystemName => "Складская система WMS (мок)";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<WarehouseAckResult> RegisterReceiptAsync(ReceiptNotice notice, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("integration.wms.receipt");
        activity?.SetTag("order.number", notice.OrderNumber);
        activity?.SetTag("lines.count", notice.Lines.Count);

        var externalRef = $"WMS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        log.LogInformation("WMS(мок): зарегистрирована приёмка заказа {Order} ({Lines} позиций) → {Ref}",
            notice.OrderNumber, notice.Lines.Count, externalRef);
        return Task.FromResult(new WarehouseAckResult(true, externalRef, null));
    }
}
