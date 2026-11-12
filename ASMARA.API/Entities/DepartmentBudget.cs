namespace ASMARA.API.Entities
{
    public class DepartmentBudget
    {
        public Guid Id { get; set; }
        public Guid DepartmentId { get; set; }
        public int FiscalYear { get; set; }
        public decimal AllocatedBudget { get; set; }
        public decimal SpentBudget { get; set; }
        public decimal CommittedBudget { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual Department Department { get; set; } = null!;
        public virtual ICollection<BudgetLog> BudgetLogs { get; set; } = new List<BudgetLog>();
    }
}
