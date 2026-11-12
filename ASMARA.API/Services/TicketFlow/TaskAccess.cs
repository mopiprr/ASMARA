
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

    public static bool CanActOn(
        Ticket ticket,
        int? stepLevel,
        Guid userId,
        IEnumerable<string> userRoleCodes)
    {
        if (stepLevel is null)
            return false;

        // Requester hanya dapat bertindak pada tiket miliknya.
        if (stepLevel == StepLevels.Requester)
            return ticket.EmployeeId == userId;

        // Mencegah Finance menyetujui tiket miliknya sendiri.
        if (ticket.EmployeeId == userId &&
            NoSelfApprovalLevels.Contains(stepLevel.Value))
        {
            return false;
        }

        // Role-based task:
        // User dengan role yang sesuai dapat bertindak,
        // meskipun CurrentTaskEmployeeId berisi Employee ID lain.
        if (LevelRoleMap.IsRoleLevel(stepLevel.Value))
        {
            return LevelRoleMap
                .LevelsFor(userRoleCodes)
                .Contains(stepLevel.Value);
        }

        // Employee-specific task:
        // Hanya karyawan yang ditugaskan yang dapat bertindak.
        if (ticket.CurrentTaskEmployeeId is not null)
        {
            return ticket.CurrentTaskEmployeeId == userId;
        }

        return false;
    }

    // Adapter untuk pemanggil lama yang masih menggunakan versi async.
    public static Task<bool> CanActOnAsync(
        Ticket ticket,
        int? stepLevel,
        Guid userId,
        IEnumerable<string> userRoleCodes,
        TicketActionEnum action,
        IEmployeeRepository employees,
        CancellationToken ct = default)
        => Task.FromResult(
            CanActOn(ticket, stepLevel, userId, userRoleCodes));
}
