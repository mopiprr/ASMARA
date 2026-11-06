using ASMARA.API.Common.Paging;
using ASMARA.API.Enums;
using ASMARA.API.Features.Dashboard.Queries.GetDashboardSummary;
using ASMARA.API.Features.Dashboard.Queries.GetMyActivityTickets;
using ASMARA.API.Features.Dashboard.Queries.GetTicketStatistics;
using ASMARA.API.Models.Dashboard;
using ASMARA.API.Models.Ticket;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(ISender sender) : ControllerBase
{

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

        var result = await sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("ticket-statistics")]
    public async Task<ActionResult<TicketStatisticsDto>> GetTicketStatistics(
        [FromQuery] TicketTypeEnum? type,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTicketStatisticsQuery(type, startDate, endDate), cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("my-activity")]
    public async Task<ActionResult<PagedResult<ActivityTicketListItemDto>>> GetMyActivity([FromQuery] GetMyActivityTicketsQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));
}