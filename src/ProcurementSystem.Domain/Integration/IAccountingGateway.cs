namespace ProcurementSystem.Domain.Integration;

/// <summary>Документ для выгрузки во внешнюю учётную систему (1С): тип, номер, контрагент, сумма.</summary>
public record AccountingDocument(string Kind, string Number, string Counterparty, decimal Amount, DateTime DateUtc);

/// <summary>Результат проведения документа во внешней системе.</summary>
public record AccountingPostResult(bool Ok, string? ExternalId, string? Error);

/// <summary>
/// Шлюз к внешней учётной системе (1С). За абстракцией — мок-адаптер (Этап 8);
/// реальная интеграция подключается новым адаптером с тем же интерфейсом, без правки бизнес-логики
/// (тот же приём, что с ISearchEngine/IEventBus).
/// </summary>
public interface IAccountingGateway
{
    /// <summary>Человекочитаемое имя системы (для статуса интеграций на экране «Настройки»).</summary>
    string SystemName { get; }

    /// <summary>Доступна ли внешняя система.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Провести (выгрузить) документ во внешнюю учётную систему.</summary>
    Task<AccountingPostResult> PostDocumentAsync(AccountingDocument doc, CancellationToken ct = default);
}
