using Asmara.API.Data;
using Asmara.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class EmployeeRepository(AsmaraDbContext context) : Repository<Employee>(context), IEmployeeRepository;