using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class ItemInventoryRepository(AsmaraDbContext context) : Repository<ItemInventory>(context), IItemInventoryRepository;
