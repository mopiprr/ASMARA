
using ASMARA.API.Data;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace ASMARA.API.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileHandler
    : IRequestHandler<GetMyProfileQuery, MyProfileResponse?>
{
    private readonly AsmaraDbContext _context;

    public GetMyProfileHandler(AsmaraDbContext context)
    {
        _context = context;
    }

    public async Task<MyProfileResponse?> Handle(
        GetMyProfileQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Account)
                .ThenInclude(a => a!.Roles)
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId,
                cancellationToken
            );

        if (employee == null || employee.Account == null)
        {
            return null;
        }

        // Get manager directly using ManagerId.
        string? managerName = null;

        if (employee.ManagerId.HasValue)
        {
            managerName = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == employee.ManagerId.Value)
                .Select(e => e.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new MyProfileResponse
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            FullName = employee.FullName,
            Email = employee.Email,
            Department = employee.Department.DepartmentName,
            Manager = managerName,
            HireDate = employee.HireDate,
            UserName = employee.Account.UserName,
            IsActive = employee.Account.IsActive,
            Roles = employee.Account.Roles
                .Select(role => role.RoleName)
                .ToList()
        };
    }
}
