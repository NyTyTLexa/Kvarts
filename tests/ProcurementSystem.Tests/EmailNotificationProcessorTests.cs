using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProcurementSystem.Domain.Commercial;
using ProcurementSystem.Domain.Integration;
using ProcurementSystem.Domain.Ordering;
using ProcurementSystem.Infrastructure.Notifications;

namespace ProcurementSystem.Tests;

public class EmailNotificationProcessorTests
{
    [Fact]
    public async Task New_event_sends_one_email_to_role_recipients()
    {
        using var db = TestDb.Create();
        db.Orders.Add(new Order
        {
            Number = "ORD-MAIL-1",
            Title = "Поставка кабеля",
            Strategy = "Balanced",
            Status = OrderStatus.Confirmed,
        });
        await db.SaveChangesAsync();

        var fake = new FakeEmailSender();
        var processor = Create(db, fake, enabled: true);

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
        using var db = TestDb.Create();
        db.Set<Approval>().Add(new Approval
        {
            Title = "Щит ТП-3",
            Strategy = "Balanced",
            Status = ApprovalStatus.ВКоммерческомБлоке,
        });
        await db.SaveChangesAsync();

        var fake = new FakeEmailSender();
        var processor = Create(db, fake, enabled: true);

        Assert.Equal(1, await processor.DispatchOnceAsync());
        Assert.Equal(0, await processor.DispatchOnceAsync());
        Assert.Single(fake.Sent);
        Assert.Equal(["commercial@procurement.local"], fake.Sent[0].To);
        Assert.Contains("согласован", fake.Sent[0].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disabled_email_sends_nothing()
    {
        using var db = TestDb.Create();
        db.Orders.Add(new Order
        {
            Number = "ORD-OFF",
            Title = "Поставка",
            Strategy = "Balanced",
            Status = OrderStatus.Confirmed,
        });
        await db.SaveChangesAsync();

        var fake = new FakeEmailSender();
        var processor = Create(db, fake, enabled: false);

        var sent = await processor.DispatchOnceAsync();

        Assert.Equal(0, sent);
        Assert.Empty(fake.Sent);
        Assert.Contains(db.Notifications, n => n.Type == "order" && n.EmailedAtUtc == null);
    }

    private static EmailNotificationProcessor Create(Infrastructure.Persistence.AppDbContext db, IEmailSender sender, bool enabled)
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
            new NotificationService(db),
            sender,
            opts,
            NullLogger<EmailNotificationProcessor>.Instance);
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
