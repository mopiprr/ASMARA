namespace ASMARA.API.Features.Profile.Queries.GetMyProfile;

public class MyProfileResponse
{
    public Guid EmployeeId { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Department { get; set; } = null!;

    public string? Manager { get; set; }

    public DateTime HireDate { get; set; }

    public string UserName { get; set; } = null!;

    public bool IsActive { get; set; }

    public List<string> Roles { get; set; } = new();
}