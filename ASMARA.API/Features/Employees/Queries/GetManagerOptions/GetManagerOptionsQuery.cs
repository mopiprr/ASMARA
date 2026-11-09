using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.Arm;

namespace ASMARA.API.Features.Employees.Queries.GetManagerOptions;

public record GetManagerOptionsQuery(Guid DepartmentId) : IRequest<IReadOnlyList<ManagerOptionDto>>;

public class GetManagerOptionsQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetManagerOptionsQuery, IReadOnlyList<ManagerOptionDto>>
{
    public async Task<IReadOnlyList<ManagerOptionDto>> Handle(GetManagerOptionsQuery request, CancellationToken ct)
    {
        var parentDepartmentId = await uow.Departments.QueryNoTracking()
            .Where(d => d.Id == request.DepartmentId)
            .Select(d => d.ParentDepartmentId)
            .FirstOrDefaultAsync(ct);

        return await uow.Employees.QueryNoTracking()
            .Where(e => e.EmployeeStatus == (byte)EmployeeStatusEnum.Available
                     && e.Account != null
                     && e.Account.IsActive
                     && (e.DepartmentId == request.DepartmentId
                         || (parentDepartmentId != null && e.DepartmentId == parentDepartmentId)))
            .OrderBy(e => e.DepartmentId == request.DepartmentId ? 0 : 1)  
            .ThenBy(e => e.FullName)
            .Select(e => new ManagerOptionDto(e.Id, e.EmployeeNumber, e.FullName, e.Department.DepartmentName))
            .ToListAsync(ct);
    }
}