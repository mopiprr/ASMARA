using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class RoleRepository(AsmaraDbContext context) : Repository<Role>(context), IRoleRepository;
