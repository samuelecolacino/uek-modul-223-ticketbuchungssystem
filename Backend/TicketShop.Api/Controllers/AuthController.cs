using Microsoft.AspNetCore.Mvc;
using TicketShop.Application.Auth;

namespace TicketShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(request, ct);
        if (response is null)
        {
            return Unauthorized(new { message = "Ungültiger Benutzername oder Passwort." });
        }

        return Ok(response);
    }
}
