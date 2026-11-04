using ASMARA.API.Data;
using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class AssetRepository(AsmaraDbContext context) : Repository<Asset>(context), IAssetRepository;
