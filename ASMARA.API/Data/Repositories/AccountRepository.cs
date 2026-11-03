using Asmara.API.Data;
using Asmara.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class AccountRepository(AsmaraDbContext context) : Repository<Account>(context), IAccountRepository;
