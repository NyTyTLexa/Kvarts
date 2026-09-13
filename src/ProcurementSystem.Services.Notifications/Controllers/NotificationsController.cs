using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Services.Notifications.Notifications;

namespace ProcurementSystem.Services.Notifications.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = "read")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NotificationDto>> List([FromQuery] bool unreadOnly, CancellationToken ct) =>
        notifications.ListAsync(UserName(), Roles(), unreadOnly, ct);

    [HttpGet("count")]
    public Task<NotificationCountDto> Count(CancellationToken ct) =>
        notifications.CountAsync(UserName(), Roles(), ct);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        await notifications.MarkReadAsync(id, UserName(), Roles(), ct) ? NoContent() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(UserName(), Roles(), ct);
        return NoContent();
    }

    private string? UserName() =>
        User.FindFirstValue("preferred_username") ?? User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private string[] Roles() => User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();
}
