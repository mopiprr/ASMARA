namespace ASMARA.API.Entities;

public partial class Ticket
{
    public Guid Id { get; set; }

    public Guid? ParentTicketId { get; set; }

    public Guid EmployeeId { get; set; }

    public string TicketNumber { get; set; } = null!;

    public byte TicketType { get; set; }

    public string Subject { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? AssignedToEmployeeId { get; set; }

    public string? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? CurrentTaskEmployeeId { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual ICollection<Ticket> InverseParentTicket { get; set; } = new List<Ticket>();

    public virtual MaintenanceDetail? MaintenanceDetail { get; set; }

    public virtual Ticket? ParentTicket { get; set; }

    public virtual ICollection<ProcurementDetail> ProcurementDetails { get; set; } = new List<ProcurementDetail>();

    public virtual ICollection<TicketWorkflowLog> TicketWorkflowLogs { get; set; } = new List<TicketWorkflowLog>();
}
