using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class WorkflowStepRepository(AsmaraDbContext context) : Repository<WorkflowStep>(context), IWorkflowStepRepository;
