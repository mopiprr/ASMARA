namespace ASMARA.API.Entities;

public partial class TicketWorkflowLog
{
    public int Id { get; set; }

    public Guid TicketId { get; set; }

    public string FromStep { get; set; } = null!;

    public string ToStep { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
}
