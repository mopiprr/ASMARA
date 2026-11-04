using ASMARA.API.Features.Profile.Queries.GetMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(
        CancellationToken cancellationToken)
    {
        var employeeIdClaim = User.FindFirst("employee_id")?.Value;

        if (!Guid.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized(new
            {
                message = "Employee identity not found."
            });
        }

        var result = await _mediator.Send(
            new GetMyProfileQuery(employeeId),
            cancellationToken
        );

        if (result == null)
        {
            return NotFound(new
            {
                message = "Employee profile not found."
            });
        }

        return Ok(result);
    }
}