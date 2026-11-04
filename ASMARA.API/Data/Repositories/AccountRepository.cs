using ASMARA.API.Data;
using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class AccountRepository(AsmaraDbContext context) : Repository<Account>(context), IAccountRepository;
