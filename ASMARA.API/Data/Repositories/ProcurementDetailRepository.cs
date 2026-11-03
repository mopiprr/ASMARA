using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class ProcurementDetailRepository(AsmaraDbContext context) : Repository<ProcurementDetail>(context), IProcurementDetailRepository;
