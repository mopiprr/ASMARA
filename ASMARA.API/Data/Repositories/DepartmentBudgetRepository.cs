using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories
{
    public class DepartmentBudgetRepository(AsmaraDbContext context) : Repository<DepartmentBudget>(context), IDepartmentBudgetRepository;
}
