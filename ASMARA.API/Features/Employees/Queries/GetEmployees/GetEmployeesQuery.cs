using ASMARA.API.Common.Paging;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Organization;
using MediatR;

namespace ASMARA.API.Features.Employees.Queries.GetEmployees;

public record GetEmployeesQuery(
    EmployeeStatusEnum? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool Desc = false,
    int Page = 1,
    int PageSize = 10)
    : IRequest<PagedResult<EmployeeListItemDto>>;

public class GetEmployeesQueryHandler(IUnitOfWork uow) : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeListItemDto>>
{
    public async Task<PagedResult<EmployeeListItemDto>> Handle(GetEmployeesQuery request, CancellationToken ct)
    {
        var query = uow.Employees.QueryNoTracking();

        if (request.Status.HasValue)
        {
            var status = (byte)request.Status.Value;
            query = query.Where(e => e.EmployeeStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(e =>
                e.EmployeeNumber.Contains(search) ||
                e.FullName.Contains(search) ||
                e.Email.Contains(search) ||
                (e.Account != null && e.Account.UserName.Contains(search)));
        }

        return await ApplySorting(query, request)
            .ThenBy(e => e.Id) 
            .Select(e => new EmployeeListItemDto(
                e.Id,
                e.EmployeeNumber,
                e.FullName,
                e.Email,
                e.Department.DepartmentName,
                e.Manager != null ? e.Manager.FullName : null,
                e.EmployeeStatus,
                e.Account != null ? e.Account.UserName : null,
                e.Account != null && e.Account.IsActive))
            .ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }

    private static IOrderedQueryable<Employee> ApplySorting(IQueryable<Employee> query, GetEmployeesQuery request)
        => request.SortBy?.ToLowerInvariant() switch
        {
            "fullname" => request.Desc
                ? query.OrderByDescending(e => e.FullName)
                : query.OrderBy(e => e.FullName),

            "department" => request.Desc
                ? query.OrderByDescending(e => e.Department.DepartmentName)
                : query.OrderBy(e => e.Department.DepartmentName),

            "status" => request.Desc
                ? query.OrderByDescending(e => e.EmployeeStatus)
                : query.OrderBy(e => e.EmployeeStatus),

            _ => request.Desc
                ? query.OrderByDescending(e => e.EmployeeNumber)
                : query.OrderBy(e => e.EmployeeNumber)
        };
}