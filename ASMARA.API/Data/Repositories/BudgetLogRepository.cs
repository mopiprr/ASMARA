using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories
{
    public class BudgetLogRepository(AsmaraDbContext context) : Repository<BudgetLog>(context), IBudgetLogRepository;
}
