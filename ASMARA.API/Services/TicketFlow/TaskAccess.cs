using ASMARA.API.Entities;

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
}
