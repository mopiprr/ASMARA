using ASMARA.API.Enums;
using static ASMARA.API.Services.TicketFlow.StepCodes;

namespace ASMARA.API.Services.TicketFlow;

public static class TransitionMap
{
    private readonly record struct Key(TicketTypeEnum Type, string From, TicketActionEnum Action);

    private static readonly Dictionary<Key, string> Transitions = BuildTransitions();
    private static readonly Dictionary<TicketTypeEnum, HashSet<string>> StepsByType = BuildStepsByType();
    private static readonly Dictionary<TicketTypeEnum, HashSet<string>> TerminalByType = BuildTerminalByType();

    public static bool TryGetNextStep(TicketTypeEnum type, string fromStep, TicketActionEnum action, out string toStep)
    {
        var found = Transitions.TryGetValue(new Key(type, fromStep, action), out var next);
        toStep = next ?? string.Empty;
        return found;
    }

    public static IReadOnlyCollection<TicketActionEnum> AllowedActions(TicketTypeEnum type, string fromStep)
        => Transitions.Keys
            .Where(k => k.Type == type && k.From == fromStep)
            .Select(k => k.Action)
            .ToList();

    public static bool IsTerminal(TicketTypeEnum type, string step)
        => TerminalByType.TryGetValue(type, out var set) && set.Contains(step);

    
    public static IReadOnlySet<string> StepsOf(TicketTypeEnum type)
        => StepsByType.TryGetValue(type, out var set) ? set : new HashSet<string>();

    private static Dictionary<Key, string> BuildTransitions()
    {
        var map = new Dictionary<Key, string>();
        void Add(TicketTypeEnum type, string from, TicketActionEnum action, string to) => map.Add(new Key(type, from, action), to);

        var proc = TicketTypeEnum.Procurement;
        var maint = TicketTypeEnum.Maintenance;

        // ===== Procurement =====
        Add(proc, Created, TicketActionEnum.Create, DepartmentApproval);

        Add(proc, DepartmentApproval, TicketActionEnum.Approve, FinanceApproval);
        Add(proc, DepartmentApproval, TicketActionEnum.Revise, EmployeeRevision);
        Add(proc, DepartmentApproval, TicketActionEnum.Reject, Rejected);

        Add(proc, FinanceApproval, TicketActionEnum.Approve, Purchasing);
        Add(proc, FinanceApproval, TicketActionEnum.Revise, EmployeeRevision);
        Add(proc, FinanceApproval, TicketActionEnum.Reject, Rejected);

        Add(proc, EmployeeRevision, TicketActionEnum.Resubmit, DepartmentApproval);
        Add(proc, EmployeeRevision, TicketActionEnum.Cancel, Cancelled);

        Add(proc, Purchasing, TicketActionEnum.ReceiveGoods, Completed);

        // ===== Maintenance =====
        Add(maint, Created, TicketActionEnum.Create, DepartmentApproval);

        Add(maint, DepartmentApproval, TicketActionEnum.Approve, ItSupportReview);
        Add(maint, DepartmentApproval, TicketActionEnum.Reject, Rejected);

        Add(maint, ItSupportReview, TicketActionEnum.AssignHandler, RequesterSendItem);
        Add(maint, RequesterSendItem, TicketActionEnum.Send, HandlerReceiveItem);
        Add(maint, HandlerReceiveItem, TicketActionEnum.Receive, RepairInProgress);

        Add(maint, RepairInProgress, TicketActionEnum.Repaired, HandlerReturnItem);
        Add(maint, RepairInProgress, TicketActionEnum.NeedPart, InventoryPartCheck);
        Add(maint, RepairInProgress, TicketActionEnum.Unrepairable, DivisionManagerReview);

        Add(maint, InventoryPartCheck, TicketActionEnum.PartAvailable, HandlerReceivePart);
        Add(maint, InventoryPartCheck, TicketActionEnum.PartUnavailable, HandlerOrderPart);
        Add(maint, HandlerReceivePart, TicketActionEnum.Receive, RepairInProgress);
        Add(maint, HandlerOrderPart, TicketActionEnum.OrderPart, WaitingForPart);
        Add(maint, WaitingForPart, TicketActionEnum.ChildCompleted, RepairInProgress);

        Add(maint, HandlerReturnItem, TicketActionEnum.Send, RequesterReceiveItem);

        Add(maint, DivisionManagerReview, TicketActionEnum.Dispose, InventoryDispose);
        Add(maint, DivisionManagerReview, TicketActionEnum.Archive, InventoryArchive);

        Add(maint, InventoryDispose, TicketActionEnum.ReplacementAvailable, RequesterReceiveItem);
        Add(maint, InventoryDispose, TicketActionEnum.ReplacementUnavailable, WaitingForReplacement);
        Add(maint, InventoryArchive, TicketActionEnum.ReplacementAvailable, RequesterReceiveItem);
        Add(maint, InventoryArchive, TicketActionEnum.ReplacementUnavailable, WaitingForReplacement);

        Add(maint, WaitingForReplacement, TicketActionEnum.ChildCompleted, InventorySendReplacement);
        Add(maint, InventorySendReplacement, TicketActionEnum.Send, RequesterReceiveItem);

        Add(maint, RequesterReceiveItem, TicketActionEnum.Receive, RequesterClose);
        Add(maint, RequesterClose, TicketActionEnum.Close, Completed);

        return map;
    }

    private static Dictionary<TicketTypeEnum, HashSet<string>> BuildStepsByType()
        => Transitions.Keys
            .GroupBy(k => k.Type)
            .ToDictionary(
                g => g.Key,
                g => g.Select(k => k.From)
                      .Concat(g.Select(k => Transitions[k]))
                      .Where(step => step != Created)
                      .ToHashSet());

    private static Dictionary<TicketTypeEnum, HashSet<string>> BuildTerminalByType()
        => StepsByType.ToDictionary(
            x => x.Key,
            x => x.Value
                .Where(step => !Transitions.Keys.Any(k => k.Type == x.Key && k.From == step))
                .ToHashSet());
}
