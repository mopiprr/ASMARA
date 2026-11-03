namespace Asmara.API.Features.Commands.Login;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public LoginUserInfo User { get; set; } = new();
}

public class LoginUserInfo
{
    public Guid AccountId { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public List<UserRoleInfo> Roles { get; set; } = new();
}

public class UserRoleInfo
{
    public int Id { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
}