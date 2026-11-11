using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Entities;
using ASMARA.API.Features.Finance.Commands.AdjustBudget;
using ASMARA.API.Features.Finance.Queries.CheckBudget;
using ASMARA.API.Features.Finance.Queries.GetBudgetLogs;
using ASMARA.API.Features.Finance.Queries.GetBudgetOverfiew;
using ASMARA.API.Features.Finance.Queries.GetDepartmentBudgets;
using ASMARA.API.Models.Finance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers
{
    [ApiController]
    [Route("api/finance")]
    [Authorize]
    public class FinanceController(ISender sender) : ControllerBase
    {
        [HttpGet("budgets")]
        public async Task<ActionResult<List<DepartmentBudgetDto>>> GetBudgets([FromQuery] int? year, CancellationToken ct)
            => Ok(await sender.Send(new GetDepartmentBudgetsQuery(year ?? DateTime.UtcNow.Year), ct));

        [HttpGet("overview")]
        public async Task<ActionResult<BudgetOverfiewDto>> GetOverview([FromQuery] int? year, CancellationToken ct)
            => Ok(await sender.Send(new GetBudgetOverfiewQuery(year ?? DateTime.UtcNow.Year), ct));

        [HttpGet("check")]
        public async Task<ActionResult<BudgetCheckResultDto>> CheckBudget(
            [FromQuery] string departmentName,
            [FromQuery] decimal estimatedTotal,
            CancellationToken ct)
            => Ok(await sender.Send(new CheckBudgetQuery(departmentName, estimatedTotal), ct));

        [HttpGet("logs")]
        public async Task<ActionResult<PagedResult<BudgetLogDto>>> GetLogs([FromQuery] GetBudgetLogsQuery query, CancellationToken ct)
            => Ok(await sender.Send(query, ct));

        [HttpPost("adjust")]
        [Authorize(Roles = $"{Roles.Finance},{Roles.Admin}")]
        public async Task<ActionResult<DepartmentBudgetDto>> AdjustBudget([FromBody] AdjustBudgetRequestDto request, CancellationToken ct)
            => Ok(await sender.Send(new AdjustBudgetCommand(request), ct));


    }
}
