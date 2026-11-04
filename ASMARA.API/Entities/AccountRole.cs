using ASMARA.API.Entities;

namespace ASMARA.API.Entities;

public class AccountRole
{
    public int RoleId { get; set; }
    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;
    public Role Role { get; set; } = null!;
}