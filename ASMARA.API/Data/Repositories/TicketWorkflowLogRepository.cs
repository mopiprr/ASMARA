using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class TicketWorkflowLogRepository(AsmaraDbContext context) : Repository<TicketWorkflowLog>(context), ITicketWorkflowLogRepository;
