using Asmara.API.Data;
using Asmara.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class WorkflowStepRepository(AsmaraDbContext context)
    : Repository<WorkflowStep>(context), IWorkflowStepRepository;
