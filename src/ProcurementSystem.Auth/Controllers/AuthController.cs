using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Auth.Keycloak;

namespace ProcurementSystem.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IKeycloakClient keycloak) : ControllerBase
{
    static readonly Regex LoginRx = new(@"^[a-zA-Z0-9._-]{3,64}$", RegexOptions.Compiled);

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResult>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var username = (req.Username ?? "").Trim();
        var email = (req.Email ?? "").Trim();
        var first = (req.FirstName ?? "").Trim();
        var last = (req.LastName ?? "").Trim();
        var password = req.Password ?? "";
        var company = string.IsNullOrWhiteSpace(req.Company) ? null : req.Company.Trim();

        if (!LoginRx.IsMatch(username))
            return BadRequest(new RegisterResult(false, "Логин: 3–64 символа, латиница, цифры, точка, _ и -."));
        if (email.Length < 5 || !email.Contains('@'))
            return BadRequest(new RegisterResult(false, "Укажите рабочий email."));
        if (first.Length < 1 || last.Length < 1)
            return BadRequest(new RegisterResult(false, "Имя и фамилия обязательны."));
        if (password.Length < 8)
            return BadRequest(new RegisterResult(false, "Пароль не короче 8 символов."));

        try
        {
            var (ok, error, conflict) = await keycloak.CreateUserAsync(username, email, first, last, password, company, ct);
            if (conflict) return Conflict(new RegisterResult(false, error ?? "Учётка уже есть."));
            if (!ok) return StatusCode(502, new RegisterResult(false, error ?? "Keycloak не принял пользователя."));
            return Ok(new RegisterResult(true, "Учётка создана. Можно входить."));
        }
        catch (Exception)
        {
            return StatusCode(503, new RegisterResult(false, "Нет связи с сервером входа (Keycloak)."));
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var username = (req.Username ?? "").Trim();
        var password = req.Password ?? "";
        if (username.Length < 1 || password.Length < 1)
            return BadRequest(new LoginResult(false, "Укажите логин и пароль.", null));

        try
        {
            var (tokens, error) = await keycloak.PasswordLoginAsync(username, password, ct);
            if (tokens is null)
                return Unauthorized(new LoginResult(false, error ?? "Вход отклонён.", null));
            return Ok(new LoginResult(true, "Ок", tokens));
        }
        catch (Exception)
        {
            return StatusCode(503, new LoginResult(false, "Нет связи с сервером входа (Keycloak).", null));
        }
    }
}

public sealed class RegisterRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Password { get; set; }
    public string? Company { get; set; }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed record RegisterResult(bool Ok, string Message);
public sealed record LoginResult(bool Ok, string Message, TokenBundle? Tokens);
