using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Employees.Queries.GetManagerOptions;

public record GetEmployeeOptionsQuery : IRequest<IReadOnlyList<EmployeeOptionDto>>;

public class GetEmployeeOptionsQueryHandler(IUnitOfWork uow) : IRequestHandler<GetEmployeeOptionsQuery, IReadOnlyList<EmployeeOptionDto>>
{
    public async Task<IReadOnlyList<EmployeeOptionDto>> Handle(GetEmployeeOptionsQuery request, CancellationToken ct)
        => await uow.Employees.QueryNoTracking()
            .Where(e => e.EmployeeStatus == (byte)EmployeeStatusEnum.Available
                     && e.Account != null
                     && e.Account.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeOptionDto(e.Id, e.EmployeeNumber, e.FullName, e.Department.DepartmentName))
            .ToListAsync(ct);
}