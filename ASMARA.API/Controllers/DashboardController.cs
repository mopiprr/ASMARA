using ASMARA.API.Features.Dashboard.Queries.GetDashboardSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken)
    {
        var employeeIdValue = User.FindFirst("employee_id")?.Value;

        if (!Guid.TryParse(employeeIdValue, out var employeeId))
        {
            return Unauthorized(new
            {
                message = "Employee ID tidak ditemukan dalam token."
            });
        }

        var employeeNumber =
            User.FindFirst("employee_number")?.Value ?? string.Empty;

        var fullName =
            User.FindFirst("name")?.Value ?? string.Empty;

        var email =
            User.FindFirst("email")?.Value ?? string.Empty;

        var roles = User.FindAll("role")
            .Select(x => x.Value)
            .Distinct()
            .ToArray();

        var query = new GetDashboardSummaryQuery(
            employeeId,
            employeeNumber,
            fullName,
            email,
            roles
        );

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }
}