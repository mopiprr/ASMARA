
using ASMARA.API.Entities;
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;

namespace ASMARA.API.Services.TicketFlow;

public class ApproverResolver(IEmployeeRepository employees) : IApproverResolver
{
    // Staff 1
    private static readonly Guid Staff1Id =
        Guid.Parse("E0000000-0000-0000-0000-000000000012");

    // Staff 2
    private static readonly Guid Staff2Id =
        Guid.Parse("E0000000-0000-0000-0000-000000000013");

    // Citra Dewanti
    private static readonly Guid CitraDewantiId =
        Guid.Parse("E0000000-0000-0000-0000-000000000011");

    // Andi Wijaya
    private static readonly Guid AndiWijayaId =
        Guid.Parse("E0000000-0000-0000-0000-000000000010");

    public async Task<Guid?> ResolveAsync(
        Ticket ticket,
        int? level,
        CancellationToken ct = default)
    {
        if (level is null)
            return null;

        // Task milik requester.
        if (level == StepLevels.Requester)
            return ticket.EmployeeId;

        // Task milik assigned handler.
        if (level == StepLevels.AssignedHandler)
        {
            if (ticket.AssignedToEmployeeId is null)
            {
                throw new BusinessRuleException(
                    "Handler has not been assigned to this ticket.");
            }

            return ticket.AssignedToEmployeeId;
        }

        var ticketType = (TicketTypeEnum)ticket.TicketType;

        var isProcurementOrMaintenance =
            ticketType == TicketTypeEnum.Procurement ||
            ticketType == TicketTypeEnum.Maintenance;

        var isInitialManagerLevel =
            level.Value == StepLevels.DirectManager ||
            level.Value == StepLevels.DepartmentManager;

        // Routing khusus approval pertama Procurement dan Maintenance.
        if (isProcurementOrMaintenance && isInitialManagerLevel)
        {
            // Procurement yang berasal dari Maintenance:
            // Approval diberikan kepada Manager IT Support
            // berdasarkan ManagerId Handler pembuat Procurement.
            if (ticketType == TicketTypeEnum.Procurement &&
                ticket.ParentTicketId.HasValue)
            {
                var handlerManagerId = await employees.GetManagerIdAsync(
                    ticket.EmployeeId,
                    ct);

                if (handlerManagerId is null)
                {
                    throw new BusinessRuleException(
                        "Approval cannot proceed: the handler does not have an assigned manager.");
                }

                return handlerManagerId;
            }

            // Staff 1 dan Staff 2 pada Procurement/Maintenance reguler
            // tetap mendapatkan approval Andi Wijaya.
            if (ticket.EmployeeId == Staff1Id ||
                ticket.EmployeeId == Staff2Id)
            {
                return AndiWijayaId;
            }

            // Pemohon lainnya langsung ke manager masing-masing.
            var managerId = await employees.GetManagerIdAsync(
                ticket.EmployeeId,
                ct);

            if (managerId is null)
            {
                throw new BusinessRuleException(
                    "Approval cannot proceed: the requester does not have an assigned manager.");
            }

            return managerId;
        }

        // Task berdasarkan role, bukan employee tertentu.
        if (LevelRoleMap.IsRoleLevel(level.Value))
            return null;

        // Workflow lainnya tetap mengikuti struktur hierarki manager.
        if (level.Value >= StepLevels.DirectManager &&
            level.Value <= StepLevels.DivisionManager)
        {
            return await WalkManagerChainAsync(
                ticket.EmployeeId,
                level.Value,
                ct);
        }

        throw new BusinessRuleException(
            $"Unknown step level {level}.");
    }

    private async Task<Guid?> WalkManagerChainAsync(
        Guid startEmployeeId,
        int hops,
        CancellationToken ct)
    {
        var current = startEmployeeId;

        for (var i = 1; i <= hops; i++)
        {
            var managerId = await employees.GetManagerIdAsync(
                current,
                ct);

            if (managerId is null)
            {
                throw new BusinessRuleException(
                    $"Approval chain is incomplete: no active manager at hop {i}.");
            }

            current = managerId.Value;
        }

        return current;
    }
}
