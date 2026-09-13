namespace ProcurementSystem.Services.Notifications.Email;

/// <summary>
/// SMTP-рассылка уведомлений (ТЗ §12). При Enabled=false отправитель — no-op,
/// стенд без почты не падает. Recipients — адреса по ролям (как в Keycloak).
/// </summary>
public class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; }
    public string From { get; set; } = "noreply@procurement.local";
    public string FromName { get; set; } = "Система закупок";
    public string? FrontendBaseUrl { get; set; }
    public int PollIntervalSeconds { get; set; } = 15;
    public Dictionary<string, string[]> Recipients { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
