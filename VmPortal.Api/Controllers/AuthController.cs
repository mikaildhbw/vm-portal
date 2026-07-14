using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VmPortal.Api.Constants;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password);
        if (!result.Success)
            return Unauthorized(new { message = result.ErrorMessage });

        Response.Cookies.Append(AuthConstants.TokenCookieName, result.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Ok(new { message = "Login erfolgreich" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthConstants.TokenCookieName);
        return Ok(new { message = "Logout erfolgreich" });
    }
}

public record LoginRequest(string Username, string Password);
