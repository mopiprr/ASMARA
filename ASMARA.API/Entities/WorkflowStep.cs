using System;
using System.Collections.Generic;

namespace Asmara.API.Entities;

public partial class WorkflowStep
{
    public int Id { get; set; }

    public int WorkflowId { get; set; }

    public string StepCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int? Level { get; set; }

    public virtual Workflow Workflow { get; set; } = null!;
}
