using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Services.TicketFlow;

public class ApproverResolver(IEmployeeRepository employees) : IApproverResolver
{
    private const int MaxChainDepth = 20;

    private static readonly string[] DepartmentApproverRoles = [Roles.DepartmentManager, Roles.DivisionManager];

    private static readonly string[] DivisionApproverRoles = [Roles.DivisionManager];

    public async Task<Guid?> ResolveAsync(Ticket ticket, int? level, CancellationToken ct = default)
    {
        if (level is null)
            return null;

        if (level == StepLevels.Requester)
            return ticket.EmployeeId;

        if (level == StepLevels.AssignedHandler)
        {
            if (ticket.AssignedToEmployeeId is null)
                throw new BusinessRuleException("Handler has not been assigned to this ticket.");

            return ticket.AssignedToEmployeeId;
        }

        if (LevelRoleMap.IsRoleLevel(level.Value))
            return null;

        switch (level.Value)
        {
            case StepLevels.DirectManager:
                return await employees.GetManagerIdAsync(ticket.EmployeeId, ct)
                    ?? throw new BusinessRuleException("Approval cannot proceed: the requester does not have a manager.");

            case StepLevels.DepartmentManager:
                // Tidak ketemu = pemohon adalah pucuk pimpinan. TicketFlowService akan melewati step ini otomatis.
                return await FindDepartmentApproverAsync(ticket.EmployeeId, ct);

            case StepLevels.DivisionManager:
                return await FindDivisionApproverAsync(ticket.EmployeeId, ct)
                    ?? throw new BusinessRuleException("Approval cannot proceed: no Division Manager was found for the requester.");
        }

        throw new BusinessRuleException($"Unknown step level {level}.");
    }

    public Task<Guid?> FindDepartmentApproverAsync(Guid requesterId, CancellationToken ct = default)
        => FindFirstAncestorWithRoleAsync(requesterId, DepartmentApproverRoles, ct);
    public async Task<Guid?> FindDivisionApproverAsync(Guid requesterId, CancellationToken ct = default)
    {
        if (await HasAnyRoleAsync(requesterId, DivisionApproverRoles, ct))
            return requesterId;

        return await FindFirstAncestorWithRoleAsync(requesterId, DivisionApproverRoles, ct);
    }

    private async Task<Guid?> FindFirstAncestorWithRoleAsync(Guid startEmployeeId, string[] roleCodes, CancellationToken ct)
    {
        var current = startEmployeeId;

        for (var i = 0; i < MaxChainDepth; i++)
        {
            var managerId = await employees.GetManagerIdAsync(current, ct);

            if (managerId is null)
                return null;

            if (await HasAnyRoleAsync(managerId.Value, roleCodes, ct))
                return managerId.Value;

            current = managerId.Value;
        }

        throw new BusinessRuleException("Approval chain is invalid: the manager hierarchy is too deep or circular.");
    }

    private Task<bool> HasAnyRoleAsync(Guid employeeId, string[] roleCodes, CancellationToken ct)
        => employees.QueryNoTracking()
            .AnyAsync(e => e.Id == employeeId
                        && e.Account != null
                        && e.Account.IsActive
                        && e.Account.Roles.Any(r => r.IsActive && roleCodes.Contains(r.RoleCode)), ct);
}
