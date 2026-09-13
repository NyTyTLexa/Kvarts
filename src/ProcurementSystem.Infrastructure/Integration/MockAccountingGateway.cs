using Microsoft.Extensions.Logging;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Infrastructure.Observability;

namespace ProcurementSystem.Infrastructure.Integration;

/// <summary>
/// Мок-адаптер учётной системы (1С), Этап 8. Имитирует проведение документа: генерирует
/// «внешний» номер и логирует. Задел под реальную интеграцию — меняется одним новым адаптером
/// IAccountingGateway без правки бизнес-логики.
/// </summary>
public class MockAccountingGateway(ILogger<MockAccountingGateway> log) : IAccountingGateway
{
    public string SystemName => "1С:Бухгалтерия (мок)";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<AccountingPostResult> PostDocumentAsync(AccountingDocument doc, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("integration.1c.post");
        activity?.SetTag("doc.kind", doc.Kind);
        activity?.SetTag("doc.number", doc.Number);

        var externalId = $"1С-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        log.LogInformation("1С(мок): проведён документ {Kind} {Number} на {Amount:F2} ₽ → {ExternalId}",
            doc.Kind, doc.Number, doc.Amount, externalId);
        return Task.FromResult(new AccountingPostResult(true, externalId, null));
    }
}
