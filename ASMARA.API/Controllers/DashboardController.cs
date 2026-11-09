using ASMARA.API.Common.Paging;
using ASMARA.API.Enums;
using ASMARA.API.Features.Dashboard.Queries.GetMyActivityTickets;
using ASMARA.API.Features.Dashboard.Queries.GetTicketStatistics;
using ASMARA.API.Models.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(ISender sender) : ControllerBase
{

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