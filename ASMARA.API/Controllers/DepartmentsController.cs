using ASMARA.API.Features.Department.Queries.GetDepartments;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentOptionDto>>> GetDepartments(CancellationToken ct)
        => Ok(await sender.Send(new GetDepartmentsQuery(), ct));
}