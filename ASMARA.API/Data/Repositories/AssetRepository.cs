using Asmara.API.Data;
using Asmara.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class AssetRepository(AsmaraDbContext context) : Repository<Asset>(context), IAssetRepository;
