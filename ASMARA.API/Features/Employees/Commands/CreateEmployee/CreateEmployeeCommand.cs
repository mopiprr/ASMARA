using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Organization;
using AccountEntity = ASMARA.API.Entities.Account;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Employees.Commands.CreateEmployee;

public record CreateEmployeeCommand(CreateEmployeeRequestDto Request) : IRequest<Guid>;

public class CreateEmployeeCommandHandler(IUnitOfWork uow, IPasswordHasher<AccountEntity> passwordHasher)
    : IRequestHandler<CreateEmployeeCommand, Guid>
{
    public async Task<Guid> Handle(CreateEmployeeCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var employeeNumber = request.EmployeeNumber.Trim();
        var email = request.Email.Trim();
        var userName = request.UserName.Trim();

        var departmentExists = await uow.Departments.QueryNoTracking()
            .AnyAsync(d => d.Id == request.DepartmentId && d.IsActive, ct);

        var department = await uow.Departments.QueryNoTracking()
            .Where(d => d.Id == request.DepartmentId && d.IsActive)
            .Select(d => new { d.ParentDepartmentId })
            .FirstOrDefaultAsync(ct);
        if (department == null)
            throw new NotFoundException("Department was not found or is inactive.");

        if (request.ManagerId is Guid managerId)
        {
            var parentDepartmentId = department.ParentDepartmentId;

            var isValidManager = await uow.Employees.QueryNoTracking()
                .AnyAsync(e => e.Id == managerId
                            && e.EmployeeStatus == (byte)EmployeeStatusEnum.Available
                            && e.Account != null
                            && e.Account.IsActive
                            && (e.DepartmentId == request.DepartmentId
                                || (parentDepartmentId != null && e.DepartmentId == parentDepartmentId)), ct);

            if (!isValidManager)
                throw new BusinessRuleException(
                    "Manager must be an active employee from the same department or its parent department.");
        }

        if (await uow.Employees.QueryNoTracking().AnyAsync(e => e.EmployeeNumber == employeeNumber, ct))
            throw new ConflictException($"Employee number '{employeeNumber}' is already used.");

        if (await uow.Employees.QueryNoTracking().AnyAsync(e => e.Email == email, ct))
            throw new ConflictException($"Email '{email}' is already used.");

        if (await uow.Accounts.QueryNoTracking().AnyAsync(a => a.UserName == userName, ct))
            throw new ConflictException($"Username '{userName}' is already used.");

        var roleIds = (request.RoleIds ?? []).Distinct().ToList();

        var roles = await uow.Roles.Query()
            .Where(r => r.IsActive && (roleIds.Contains(r.Id) || r.RoleCode == Roles.Employee))
            .ToListAsync(ct);

        var foundRoleIds = roles.Select(r => r.Id).ToHashSet();

        if (roleIds.Any(id => !foundRoleIds.Contains(id)))
            throw new BusinessRuleException("One or more selected roles are invalid or inactive.");

        if (!roles.Any(r => r.RoleCode == Roles.Employee))
            throw new BusinessRuleException("Base role EMPLOYEE is not configured or inactive.");

        var now = DateTime.UtcNow;

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            DepartmentId = request.DepartmentId,
            ManagerId = request.ManagerId,
            EmployeeNumber = employeeNumber,
            FullName = request.FullName.Trim(),
            Email = email,
            EmployeeStatus = (byte)EmployeeStatusEnum.Available,
            HireDate = request.HireDate.ToDateTime(TimeOnly.MinValue),
            CreatedAt = now
        };

        var account = new AccountEntity
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            UserName = userName,
            IsActive = true,
            CreatedAt = now,
            Roles = roles
        };

        account.PasswordHashed = passwordHasher.HashPassword(account, request.Password);

        uow.Employees.Add(employee);
        uow.Accounts.Add(account);

        await uow.SaveChangesAsync(ct);

        return employee.Id;
    }
}