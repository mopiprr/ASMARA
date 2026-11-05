using ASMARA.API.Entities;
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;

namespace ASMARA.API.Services.TicketFlow;

public class ApproverResolver(IEmployeeRepository employees) : IApproverResolver
{
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

        if (level is >= StepLevels.DirectManager and <= StepLevels.DivisionManager)
            return await WalkManagerChainAsync(ticket.EmployeeId, level.Value, ct);

        throw new BusinessRuleException($"Unknown step level {level}.");
    }

    // private async Task<Guid?> WalkManagerChainAsync(Guid startEmployeeId, int hops, CancellationToken ct)
    // {
    //     var current = startEmployeeId;
    //     for (var i = 1; i <= hops; i++)
    //     {
    //         var managerId = await employees.GetManagerIdAsync(current, ct);
    //         if (managerId is null)
    //         {
    //             if (i > 1) return current;
    //             throw new BusinessRuleException($"Approval chain is incomplete: no active manager at hop {i}.");
    //         }
    //         current = managerId.Value;
    //     }
    //     return current;
    // }

    private async Task<Guid?> WalkManagerChainAsync(Guid startEmployeeId, int targetLevel, CancellationToken ct)
    {
        var current = startEmployeeId;
        var visited = new HashSet<Guid> { current };

        // 1. DirectManager (Level 1): 1 hop ke atasan langsung
        if (targetLevel == StepLevels.DirectManager)
        {
            var managerId = await employees.GetManagerIdAsync(current, ct);
            if (managerId is null)
                throw new BusinessRuleException("Approval chain is incomplete: no active direct manager.");

            return managerId;
        }

        // 2. DepartmentManager (Level 2) atau DivisionManager (Level 3): Telusuri hierarki berdasarkan peran
        while (true)
        {
            var (managerId, _) = await employees.GetManagerAndRolesAsync(current, ct);
            if (managerId is null)
            {
                if (current != startEmployeeId)
                    return current;

                throw new BusinessRuleException("Approval chain is incomplete: no active manager found.");
            }

            if (!visited.Add(managerId.Value))
                throw new BusinessRuleException("Circular manager hierarchy detected.");

            current = managerId.Value;
            var (_, currentRoles) = await employees.GetManagerAndRolesAsync(current, ct);

            if (targetLevel == StepLevels.DepartmentManager)
            {
                // Jika atasan memiliki peran Department Manager (ROL-02) atau Division Manager (ROL-03), tetapkan sebagai approver
                if (currentRoles.Contains(Roles.DepartmentManager) || currentRoles.Contains(Roles.DivisionManager))
                    return current;
            }
            else if (targetLevel == StepLevels.DivisionManager)
            {
                if (currentRoles.Contains(Roles.DivisionManager))
                    return current;
            }
        }
    }
}
