namespace ASMARA.API.Services.TicketFlow;

public static class StepCodes
{
    public const string Created = "CREATED";
    public const string DepartmentApproval = "DEPARTMENT_APPROVAL";
    public const string Completed = "COMPLETED";
    public const string Rejected = "REJECTED";
    public const string FinanceApproval = "FINANCE_APPROVAL";
    public const string EmployeeRevision = "EMPLOYEE_REVISION";
    public const string Purchasing = "PURCHASING";
    public const string Cancelled = "CANCELLED";
    public const string ItSupportReview = "IT_SUPPORT_REVIEW";
    public const string RequesterSendItem = "REQUESTER_SEND_ITEM";
    public const string HandlerReceiveItem = "HANDLER_RECEIVE_ITEM";
    public const string RepairInProgress = "REPAIR_IN_PROGRESS";
    public const string InventoryPartCheck = "INVENTORY_PART_CHECK";
    public const string HandlerReceivePart = "HANDLER_RECEIVE_PART";
    public const string HandlerOrderPart = "HANDLER_ORDER_PART";
    public const string WaitingForPart = "WAITING_FOR_PART";
    public const string HandlerReturnItem = "HANDLER_RETURN_ITEM";
    public const string DivisionManagerReview = "DIVISION_MANAGER_REVIEW";
    public const string InventoryDispose = "INVENTORY_DISPOSE";
    public const string InventoryArchive = "INVENTORY_ARCHIVE";
    public const string WaitingForReplacement = "WAITING_FOR_REPLACEMENT";
    public const string InventorySendReplacement = "INVENTORY_SEND_REPLACEMENT";
    public const string RequesterReceiveItem = "REQUESTER_RECEIVE_ITEM";
    public const string RequesterClose = "REQUESTER_CLOSE";
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