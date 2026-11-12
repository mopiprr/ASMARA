namespace ASMARA.API.Entities;

public partial class Employee
{
    public Guid Id { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid? ManagerId { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public byte EmployeeStatus { get; set; }

    public DateTime HireDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? TerminatedAt { get; set; }

    public virtual Account? Account { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Employee> InverseManager { get; set; } = new List<Employee>();

    public virtual Employee? Manager { get; set; }

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
