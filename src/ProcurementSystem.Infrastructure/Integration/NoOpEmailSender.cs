using Microsoft.Extensions.Logging;
using ProcurementSystem.Domain.Integration;

namespace ProcurementSystem.Infrastructure.Integration;

/// <summary>Заглушка, когда Email:Enabled=false — вызовы игнорируются, процесс не падает.</summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> log) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        log.LogDebug("Email отключён, письмо «{Subject}» не отправлено", message.Subject);
        return Task.CompletedTask;
    }
}
