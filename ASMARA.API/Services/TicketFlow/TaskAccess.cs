using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;

namespace ASMARA.API.Services.TicketFlow;

public static class TaskAccess
{
    private static readonly HashSet<int> NoSelfApprovalLevels = [StepLevels.Finance];

    public static bool CanActOn(Ticket ticket, int? stepLevel, Guid userId, IEnumerable<string> userRoleCodes)
    {
        if (ticket.CurrentTaskEmployeeId is not null)
            return ticket.CurrentTaskEmployeeId == userId;

        if (stepLevel is null)
            return false;

        if (stepLevel == StepLevels.Requester)
            return ticket.EmployeeId == userId;

        if (ticket.EmployeeId == userId && NoSelfApprovalLevels.Contains(stepLevel.Value))
            return false;

        if (LevelRoleMap.IsRoleLevel(stepLevel.Value))
            return LevelRoleMap.LevelsFor(userRoleCodes).Contains(stepLevel.Value);

        return false;
    }

    // SEMENTARA (rollback 6 Nov 2026): adaptor untuk pemanggil yang masih memakai versi async
    // (GetPendingTicketsQuery, GetPendingTicketByIdQuery, GetProcurementTicketsQueryHandler).
    // Aturannya SAMA PERSIS dengan CanActOn di atas; 'action' dan 'employees' diabaikan.
    // Hapus setelah pemanggil diganti ke CanActOn.
    public static Task<bool> CanActOnAsync(
        Ticket ticket, int? stepLevel, Guid userId, IEnumerable<string> userRoleCodes,
        TicketActionEnum action, IEmployeeRepository employees, CancellationToken ct = default)
        => Task.FromResult(CanActOn(ticket, stepLevel, userId, userRoleCodes));
}
