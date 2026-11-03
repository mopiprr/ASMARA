using System;
using System.Collections.Generic;

namespace Asmara.API.Entities;

public partial class Workflow
{
    public int Id { get; set; }

    public string WorkflowCode { get; set; } = null!;

    public string WorkflowName { get; set; } = null!;

    public byte TicketType { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<WorkflowStep> WorkflowSteps { get; set; } = new List<WorkflowStep>();
}
