using ASMARA.API.Features.Account.Commands.ChangePassword;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "Current password and new password are required."
            });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "New password must be at least 8 characters."
            });
        }

        // Get AccountId from JWT subject claim
        var accountIdClaim =
            User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(accountIdClaim, out var accountId))
        {
            return Unauthorized(new
            {
                message = "Invalid account identity."
            });
        }

        var result = await _mediator.Send(
            new ChangePasswordCommand
            {
                AccountId = accountId,
                CurrentPassword = request.CurrentPassword,
                NewPassword = request.NewPassword
            },
            cancellationToken);

        return result switch
        {
            ChangePasswordResult.Success => Ok(new
            {
                message = "Password changed successfully."
            }),

            ChangePasswordResult.InvalidCurrentPassword =>
                BadRequest(new
                {
                    message = "Current password is incorrect."
                }),

            ChangePasswordResult.SamePassword =>
                BadRequest(new
                {
                    message = "New password must be different from current password."
                }),

            _ => BadRequest()
        };
    }
}