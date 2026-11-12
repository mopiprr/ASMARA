
namespace ASMARA.API.Services.TicketFlow;

public static class StepCodes
{
    public const string Created = "CREATED";

    // Procurement and shared steps
    public const string DepartmentApproval = "DEPARTMENT_APPROVAL";
    public const string Completed = "COMPLETED";
    public const string Rejected = "REJECTED";
    public const string FinanceApproval = "FINANCE_APPROVAL";
    public const string EmployeeRevision = "EMPLOYEE_REVISION";
    public const string Purchasing = "PURCHASING";
    public const string Cancelled = "CANCELLED";

    // IT Support
    public const string ItSupportReview = "IT_SUPPORT_REVIEW";
    public const string ItSupportResultReview = "IT_SUPPORT_RESULT_REVIEW";
    public const string ItSupportDisposeReview = "IT_SUPPORT_DISPOSE_REVIEW";

    // Requester and Handler
    public const string RequesterSendItem = "REQUESTER_SEND_ITEM";
    public const string HandlerReceiveItem = "HANDLER_RECEIVE_ITEM";
    public const string RepairInProgress = "REPAIR_IN_PROGRESS";
    public const string HandlerReturnItem = "HANDLER_RETURN_ITEM";
    public const string HandlerReceivePart = "HANDLER_RECEIVE_PART";
    public const string HandlerOrderPart = "HANDLER_ORDER_PART";

    // Inventory
    public const string InventoryPartCheck = "INVENTORY_PART_CHECK";
    public const string InventoryOrderPart = "INVENTORY_ORDER_PART";
    public const string InventorySendPart = "INVENTORY_SEND_PART";
    public const string InventoryDispose = "INVENTORY_DISPOSE";
    public const string InventoryArchive = "INVENTORY_ARCHIVE";
    public const string InventorySendReplacement = "INVENTORY_SEND_REPLACEMENT";

    // Waiting steps
    public const string WaitingForPart = "WAITING_FOR_PART";
    public const string WaitingForReplacement = "WAITING_FOR_REPLACEMENT";

    // Division Manager
    public const string DivisionManagerReview = "DIVISION_MANAGER_REVIEW";

    // Requester receives and closes ticket
    public const string RequesterReceiveItem = "REQUESTER_RECEIVE_ITEM";
    public const string RequesterClose = "REQUESTER_CLOSE";

    // Handler
    public const string HandlerSendToInventory = "HANDLER_SEND_TO_INVENTORY";

    // Inventory
    public const string InventoryReplacementCheck = "INVENTORY_REPLACEMENT_CHECK";


    public const string HandlerSendToInventoryDispose =
        "HANDLER_SEND_TO_INVENTORY_DISPOSE";

    public const string HandlerSendToInventoryArchive =
        "HANDLER_SEND_TO_INVENTORY_ARCHIVE";

}

public static class StepLevels
{
    public const int Requester = 0;
    public const int DirectManager = 1;
    public const int DepartmentManager = 2;
    public const int DivisionManager = 3;
    public const int Finance = 101;
    public const int Purchasing = 102;
    public const int Inventory = 201;
    public const int ItSupport = 202;
    public const int AssignedHandler = 900;
}
