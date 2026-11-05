using ASMARA.API.Entities;
using ASMARA.API.Common.Exceptions;
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

    private async Task<Guid?> WalkManagerChainAsync(Guid startEmployeeId, int hops, CancellationToken ct)
    {
        var current = startEmployeeId;

        for (var i = 1; i <= hops; i++)
        {
            var managerId = await employees.GetManagerIdAsync(current, ct);

            // if (managerId is null)
            //     throw new BusinessRuleException($"Approval chain is incomplete: no active manager at hop {i}.");
            if (managerId is null)
            {
                if (i > 1)
                    return current;

                throw new BusinessRuleException($"Approval chain is incomplete: no active manager at hop {i}.");
            }

            current = managerId.Value;
        }

        return current;
    }
}
