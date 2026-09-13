using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Infrastructure.Notifications;
using ProcurementSystem.Infrastructure.Observability;

namespace ProcurementSystem.Infrastructure.Integration;

/// <summary>SMTP-отправка (MailHog на стенде: Host=mailhog, Port=1025, без SSL и auth).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> opts, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("integration.email.send");
        activity?.SetTag("email.to.count", message.To.Count);
        activity?.SetTag("email.subject", message.Subject);

        var o = opts.Value;
        if (!o.Enabled || message.To.Count == 0)
            return;

        using var mail = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(o.FromName)
                ? new MailAddress(o.From)
                : new MailAddress(o.From, o.FromName, Encoding.UTF8),
            Subject = message.Subject,
            Body = message.Body,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        foreach (var to in message.To)
            mail.To.Add(to);

#pragma warning disable SYSLIB0014 // SmtpClient — без лишних пакетов; для MailHog достаточно
        using var client = new SmtpClient(o.Host, o.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = o.UseSsl,
        };
#pragma warning restore SYSLIB0014

        await client.SendMailAsync(mail, ct);
        log.LogInformation("Email: «{Subject}» → {To}", message.Subject, string.Join(", ", message.To));
    }
}
