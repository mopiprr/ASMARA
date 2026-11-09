using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Department.Queries.GetDepartments;

public record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentOptionDto>>;

public class GetDepartmentsQueryHandler(IUnitOfWork uow) : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentOptionDto>>
{
    public async Task<IReadOnlyList<DepartmentOptionDto>> Handle(GetDepartmentsQuery request, CancellationToken ct)
        => await uow.Departments.QueryNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DepartmentName)
            .Select(d => new DepartmentOptionDto(d.Id, d.DepartmentCode, d.DepartmentName))
            .ToListAsync(ct);
}