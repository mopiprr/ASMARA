
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;

namespace ASMARA.API.Services.TicketFlow;

public static class TaskAccess
{
    private static readonly HashSet<int> NoSelfApprovalLevels =
    [
        StepLevels.Finance
    ];

    public static async Task<bool> CanActOnAsync(
        Ticket ticket,
        int? stepLevel,
        Guid userId,
        IEnumerable<string> userRoleCodes,
        TicketActionEnum action,
        IEmployeeRepository employees,
        CancellationToken ct = default)
    {
        if (stepLevel is null)
            return false;

        var ticketType = (TicketTypeEnum)ticket.TicketType;

        // Requester hanya boleh melakukan aksi pada tahap Requester.
        if (stepLevel == StepLevels.Requester)
            return ticket.EmployeeId == userId;

        // Employee pembuat Procurement tidak boleh melakukan
        // approval atau keputusan atas tiketnya sendiri.
        if (ticketType == TicketTypeEnum.Procurement &&
            ticket.EmployeeId == userId)
        {
            return false;
        }

        // Pertahankan aturan self-approval Finance untuk workflow lain.
        if (ticket.EmployeeId == userId &&
            NoSelfApprovalLevels.Contains(stepLevel.Value))
        {
            return false;
        }

        // Procurement Department Approval:
        // Direct Manager dan seluruh atasannya boleh
        // Approve, Revise, maupun Reject.
        if (ticketType == TicketTypeEnum.Procurement &&
            stepLevel == StepLevels.DepartmentManager)
        {
            return await CanActOnProcurementDepartmentApprovalAsync(
                ticket,
                userId,
                action,
                employees,
                ct);
        }

        // Role-based task: Finance, Purchasing, Inventory, IT Support.
        if (LevelRoleMap.IsRoleLevel(stepLevel.Value))
        {
            return LevelRoleMap
                .LevelsFor(userRoleCodes)
                .Contains(stepLevel.Value);
        }

        // Individual task: hanya employee yang ditunjuk.
        if (ticket.CurrentTaskEmployeeId is not null)
            return ticket.CurrentTaskEmployeeId == userId;

        return false;
    }

    private static async Task<bool>
        CanActOnProcurementDepartmentApprovalAsync(
            Ticket ticket,
            Guid userId,
            TicketActionEnum action,
            IEmployeeRepository employees,
            CancellationToken ct)
    {
        var currentEmployeeId = ticket.EmployeeId;

        var visited = new HashSet<Guid>
        {
            currentEmployeeId
        };

        while (true)
        {
            var managerId = await employees.GetManagerIdAsync(
                currentEmployeeId,
                ct);

            if (managerId is null)
                return false;

            // Mencegah circular ManagerId.
            if (!visited.Add(managerId.Value))
                return false;

            if (managerId.Value == userId)
            {
                return action is
                    TicketActionEnum.Approve or
                    TicketActionEnum.Revise or
                    TicketActionEnum.Reject;
            }

            currentEmployeeId = managerId.Value;
        }
    }
}
