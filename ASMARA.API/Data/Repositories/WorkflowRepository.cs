using Asmara.API.Data;
using Asmara.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class WorkflowRepository(AsmaraDbContext context)
    : Repository<Workflow>(context), IWorkflowRepository;
