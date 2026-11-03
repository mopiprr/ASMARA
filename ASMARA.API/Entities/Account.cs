namespace Asmara.API.Entities;

public partial class Account
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string UserName { get; set; } = null!;

    public string PasswordHashed { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
