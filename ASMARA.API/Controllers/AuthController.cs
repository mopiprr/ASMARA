using ASMARA.API.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asmara.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    private const string RefreshCookieName = "asmara_refresh";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        SetRefreshCookie(result);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];

        var result = await mediator.Send(
            new RefreshTokenCommand(refreshToken),
            cancellationToken);

        if (result == null)
        {
            DeleteRefreshCookie();

            return Unauthorized(new
            {
                message = "Session expired. Please login again."
            });
        }

        SetRefreshCookie(result);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];

        await mediator.Send(new LogoutCommand(refreshToken), cancellationToken);

        DeleteRefreshCookie();

        return NoContent();
    }

    private CookieOptions BuildCookieOptions() => new()
    {
        HttpOnly = true,            
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"        
    };

    private void SetRefreshCookie(LoginResponse result)
    {
        var options = BuildCookieOptions();

        if (result.IsPersistent)
        {
            options.Expires = new DateTimeOffset(result.RefreshTokenExpiresAt, TimeSpan.Zero);
        }

        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, options);
    }

    private void DeleteRefreshCookie()
        => Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions());
}