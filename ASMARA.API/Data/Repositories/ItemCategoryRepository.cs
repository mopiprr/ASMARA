using Asmara.API.Data;
using Asmara.API.Entities;

namespace ASMARA.API.Data.Repositories;

public class ItemCategoryRepository(AsmaraDbContext context) : Repository<ItemCategory>(context), IItemCategoryRepository;
