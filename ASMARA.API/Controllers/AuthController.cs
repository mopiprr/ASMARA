using ASMARA.API.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Asmara.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Email atau password tidak valid."
            });
        }

        return Ok(result);
    }
}