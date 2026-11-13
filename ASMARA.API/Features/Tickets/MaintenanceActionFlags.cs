using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;

namespace ASMARA.API.Features.Tickets;

public static class MaintenanceActionFlags
{
    public static TicketResponseDto Apply(TicketResponseDto dto, bool canAct, string currentStep)
    {
        var allowed = TransitionMap.AllowedActions(TicketTypeEnum.Maintenance, currentStep);

        bool Can(TicketActionEnum action) => canAct && allowed.Contains(action);

        var isPartStep = currentStep == StepCodes.HandlerReceivePart;
        var isSendReplacementStep = currentStep == StepCodes.InventorySendReplacement;

        return dto with
        {
            CanApprove = Can(TicketActionEnum.Approve),
            CanReject = Can(TicketActionEnum.Reject),
            CanAssignHandler = Can(TicketActionEnum.AssignHandler),

            // Send biasa (requester/handler). Kirim pengganti lewat send-replacement.
            CanSendItem = !isSendReplacementStep && Can(TicketActionEnum.Send),
            CanSendReplacement = isSendReplacementStep && Can(TicketActionEnum.Send),

            // Receive barang biasa vs receive part.
            CanReceiveItem = !isPartStep && Can(TicketActionEnum.Receive),
            CanReceivePart = isPartStep && Can(TicketActionEnum.Receive),

            CanSubmitRepairResult = Can(TicketActionEnum.Repaired)
                                    || Can(TicketActionEnum.NeedPart)
                                    || Can(TicketActionEnum.Unrepairable),

            // Inventory di INVENTORY_PART_CHECK.
            // Tersedia -> POST send-part (kurangi stok), tidak tersedia -> POST part-check { available: false }.
            CanCheckPart = Can(TicketActionEnum.PartAvailable) || Can(TicketActionEnum.PartUnavailable),

            CanOrderPart = Can(TicketActionEnum.OrderPart),

            CanArchive = Can(TicketActionEnum.Archive),
            CanDispose = Can(TicketActionEnum.Dispose),

            CanProcessReplacement = Can(TicketActionEnum.ReplacementAvailable)
                                    || Can(TicketActionEnum.ReplacementUnavailable)
        };
    }
}