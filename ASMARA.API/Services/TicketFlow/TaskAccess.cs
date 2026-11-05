
using ASMARA.API.Entities;

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

        // Requester hanya dapat melakukan aksi pada tiket miliknya.
        if (stepLevel == StepLevels.Requester)
            return ticket.EmployeeId == userId;

        // Mencegah requester menyetujui tiketnya sendiri sebagai Finance.
        if (ticket.EmployeeId == userId &&
            NoSelfApprovalLevels.Contains(stepLevel.Value))
            return false;

        // Role-based task: semua pengguna dengan role terkait
        // dapat menjalankan aksi selama tahap tersebut aktif.
        if (LevelRoleMap.IsRoleLevel(stepLevel.Value))
        {
            return LevelRoleMap
                .LevelsFor(userRoleCodes)
                .Contains(stepLevel.Value);
        }

        // Individual task: hanya Employee yang ditunjuk.
        if (ticket.CurrentTaskEmployeeId is not null)
            return ticket.CurrentTaskEmployeeId == userId;

        return false;
    }
}
