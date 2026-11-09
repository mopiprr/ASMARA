using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Auth;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Employees.Queries.GetEmployeeById;

public record GetEmployeeByIdQuery(Guid Id) : IRequest<EmployeeDetailDto>;

public class GetEmployeeByIdQueryHandler(IUnitOfWork uow) : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto>
{
    private const byte Available = (byte)EmployeeStatusEnum.Available;
    private const byte AssetInUse = (byte)AssetStatusEnum.InUse;
    private const byte AssetUnderMaintenance = (byte)AssetStatusEnum.UnderMaintenance;

    public async Task<EmployeeDetailDto> Handle(GetEmployeeByIdQuery request, CancellationToken ct)
        => await uow.Employees.QueryNoTracking()
            .Where(e => e.Id == request.Id)
            .Select(e => new EmployeeDetailDto(
                e.Id,
                e.EmployeeNumber,
                e.FullName,
                e.Email,
                e.DepartmentId,
                e.Department.DepartmentName,
                e.ManagerId,
                e.Manager != null ? e.Manager.FullName : null,
                e.EmployeeStatus,
                e.HireDate,
                e.CreatedAt,
                e.TerminatedAt,
                e.InverseManager.Count(s => s.EmployeeStatus == Available),
                e.Account == null
                    ? null
                    : new EmployeeAccountDto(
                        e.Account.Id,
                        e.Account.UserName,
                        e.Account.IsActive,
                        e.Account.CreatedAt,
                        e.Account.Roles
                            .OrderBy(r => r.RoleCode)
                            .Select(r => new RoleOptionDto(r.Id, r.RoleCode, r.RoleName, r.Description))
                            .ToList()),
                e.Assets
                    .Where(a => a.Status == AssetInUse || a.Status == AssetUnderMaintenance)
                    .OrderBy(a => a.AssetTag)
                    .Select(a => new EmployeeAssetDto(a.Id, a.AssetTag, a.Item.ItemName, a.Status))
                    .ToList()))
            .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException($"Employee {request.Id} was not found.");
}