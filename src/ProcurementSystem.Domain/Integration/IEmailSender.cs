namespace ProcurementSystem.Domain.Integration;

/// <summary>Письмо во внешнюю почтовую систему (SMTP / MailHog).</summary>
public record EmailMessage(IReadOnlyList<string> To, string Subject, string Body);

/// <summary>
/// Отправка email. За абстракцией — SMTP-адаптер или no-op (Email:Enabled=false),
/// без правки бизнес-логики согласований/счетов/заказов (тот же приём, что IAccountingGateway).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
