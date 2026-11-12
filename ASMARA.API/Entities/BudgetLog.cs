namespace ASMARA.API.Entities
{
    public class BudgetLog
    {
        public Guid Id { get; set; }
        public Guid DepartmentBudgetId { get; set; }
        public Guid? TicketId { get; set; }
        public Guid? RelatedDepartmentId { get; set; }
        public string LogNumber { get; set; } = null!;
        public byte ChangeType { get; set; }
        public decimal Amount { get; set; }
        public decimal PreviousAllocated { get; set; }
        public decimal NewAllocated { get; set; }
        public decimal PreviousRemaining { get; set; }
        public decimal NewRemaining { get; set; }
        public string Description { get; set; } = null!;
        public Guid CreatedByEmployeeId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual DepartmentBudget DepartmentBudget { get; set; } = null!;
        public virtual Ticket? Ticket { get; set; }
    }
}
