using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class MaintenanceDetailRepository(AsmaraDbContext context) : Repository<MaintenanceDetail>(context), IMaintenanceDetailRepository;
