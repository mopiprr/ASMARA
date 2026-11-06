using ASMARA.API.Enums;

namespace ASMARA.API.Services.TicketFlow;

public class TicketHistoryMapper
{
    private static readonly Dictionary<TicketActionEnum, string> Labels = new()
    {
        [TicketActionEnum.Create] = "Submit",
        [TicketActionEnum.Approve] = "Approve",
        [TicketActionEnum.Revise] = "Revise",
        [TicketActionEnum.Reject] = "Reject",
        [TicketActionEnum.Resubmit] = "Resubmit",
        [TicketActionEnum.Cancel] = "Cancel",
        [TicketActionEnum.ReceiveGoods] = "Receive of Goods",
        [TicketActionEnum.AssignHandler] = "Assign Handler",
        [TicketActionEnum.Send] = "Send Item",
        [TicketActionEnum.Receive] = "Receive Item",
        [TicketActionEnum.Repaired] = "Repaired",
        [TicketActionEnum.NeedPart] = "Need Part",
        [TicketActionEnum.Unrepairable] = "Unrepairable",
        [TicketActionEnum.PartAvailable] = "Part Available",
        [TicketActionEnum.PartUnavailable] = "Part Unavailable",
        [TicketActionEnum.OrderPart] = "Order Part",
        [TicketActionEnum.ChildCompleted] = "Child Ticket Completed",
        [TicketActionEnum.Dispose] = "Dispose",
        [TicketActionEnum.Archive] = "Archive",
        [TicketActionEnum.ReplacementAvailable] = "Replacement Available",
        [TicketActionEnum.ReplacementUnavailable] = "Replacement Unavailable",
        [TicketActionEnum.ChildRejected] = "Child Ticket Rejected",
        [TicketActionEnum.Close] = "Close"
    };

    public static TicketActionEnum? ResolveAction(TicketTypeEnum type, string fromStep, string toStep)
    {
        foreach (var action in TransitionMap.AllowedActions(type, fromStep))
        {
            if (TransitionMap.TryGetNextStep(type, fromStep, action, out var to) && to == toStep)
                return action;
        }

        if (fromStep is StepCodes.WaitingForPart or StepCodes.WaitingForReplacement)
            return TicketActionEnum.ChildRejected;

        return null;
    }

    public static string LabelOf(TicketActionEnum? action, string fromStep, string toStep)
        => action is not null && Labels.TryGetValue(action.Value, out var label)
            ? label
            : $"{fromStep} → {toStep}";
}