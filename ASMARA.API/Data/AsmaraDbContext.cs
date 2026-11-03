using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data;

public class AsmaraDbContext(DbContextOptions<AsmaraDbContext> options) : DbContext(options)
{
}
