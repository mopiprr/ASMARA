using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class DepartmentRepository(AsmaraDbContext context) : Repository<Department>(context), IDepartmentRepository;