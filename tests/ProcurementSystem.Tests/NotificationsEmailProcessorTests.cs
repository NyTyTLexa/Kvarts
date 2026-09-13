using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Services.Notifications.Email;
using ProcurementSystem.Services.Notifications.Notifications;
using ProcurementSystem.Services.Notifications.Persistence;

namespace ProcurementSystem.Tests;

public class NotificationsEmailProcessorTests
{
    [Fact]
    public async Task New_event_sends_one_email_to_role_recipients()
    {
        using var db = CreateDb();
        var item = new OpenWorkItem(
            "order",
            "manager",
            "Заказ в работе",
            "ORD-MAIL-1: Поставка кабеля, статус Confirmed.",
            "Order",
            Guid.NewGuid(),
            DateTime.UtcNow);
        var fake = new FakeEmailSender();
        var processor = Create(db, fake, enabled: true, item);

        var sent = await processor.DispatchOnceAsync();

        Assert.Equal(1, sent);
        var mail = Assert.Single(fake.Sent);
        Assert.Equal(["manager@procurement.local"], mail.To);
        Assert.Contains("Заказ", mail.Subject);
        Assert.Contains("ORD-MAIL-1", mail.Body);
        Assert.Contains("http://localhost:5173/orders/", mail.Body);
        Assert.All(db.Notifications, n => Assert.NotNull(n.EmailedAtUtc));
    }

    [Fact]
    public async Task Second_pass_does_not_send_duplicate()
    {
        using var db = CreateDb();
        var item = new OpenWorkItem(
            "approval",
            "commercial",
            "КП ожидает согласования",
            "Щит ТП-3: требуется решение коммерческого блока.",
            "Approval",
            Guid.NewGuid(),
            DateTime.UtcNow);
        var fake = new FakeEmailSender();
        var processor = Create(db, fake, enabled: true, item);

        Assert.Equal(1, await processor.DispatchOnceAsync());
        Assert.Equal(0, await processor.DispatchOnceAsync());
        Assert.Single(fake.Sent);
        Assert.Equal(["commercial@procurement.local"], fake.Sent[0].To);
        Assert.Contains("согласован", fake.Sent[0].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disabled_email_sends_nothing()
    {
        using var db = CreateDb();
        var item = new OpenWorkItem(
            "order",
            "manager",
            "Заказ в работе",
            "ORD-OFF: Поставка, статус Confirmed.",
            "Order",
            Guid.NewGuid(),
            DateTime.UtcNow);
        var fake = new FakeEmailSender();
        var processor = Create(db, fake, enabled: false, item);

        var sent = await processor.DispatchOnceAsync();

        Assert.Equal(0, sent);
        Assert.Empty(fake.Sent);
        Assert.Contains(db.Notifications, n => n.Type == "order" && n.EmailedAtUtc == null);
    }

    private static NotificationsDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EmailNotificationProcessor Create(
        NotificationsDbContext db, IEmailSender sender, bool enabled, params OpenWorkItem[] items)
    {
        var opts = Options.Create(new EmailOptions
        {
            Enabled = enabled,
            FrontendBaseUrl = "http://localhost:5173",
            Recipients = new Dictionary<string, string[]>
            {
                ["manager"] = ["manager@procurement.local"],
                ["commercial"] = ["commercial@procurement.local"],
                ["accounting"] = ["accounting@procurement.local"],
                ["warehouse"] = ["warehouse@procurement.local"],
            },
        });
        return new EmailNotificationProcessor(
            db,
            new NotificationService(db, new FakeOpenWorkSource(items)),
            sender,
            opts,
            NullLogger<EmailNotificationProcessor>.Instance);
    }

    private sealed class FakeOpenWorkSource(params OpenWorkItem[] items) : IOpenWorkSource
    {
        public Task<IReadOnlyList<OpenWorkItem>> GetOpenWorkAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OpenWorkItem>>(items);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
