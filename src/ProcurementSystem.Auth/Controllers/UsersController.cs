using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Auth.Keycloak;

namespace ProcurementSystem.Auth.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "admin")]
public class UsersController(IKeycloakClient keycloak) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct)
    {
        try
        {
            var users = await keycloak.ListUsersAsync(ct);
            return users
                .Select(u => new UserDto(u.UserName, u.Email, u.DisplayName, u.Roles, u.Enabled, null))
                .OrderBy(u => u.UserName)
                .ToList();
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Не удалось получить пользователей из Keycloak: " + ex.Message });
        }
    }
}

public record UserDto(
    string UserName,
    string? Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool Enabled,
    DateTime? LastActivityUtc);
