namespace ASMARA.API.Models.Auth;

public class RoleDto
{

}

public record RoleResponseDto(
    int Id,
    string RoleCode,
    string RoleName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt
    );

public record RoleOptionDto(int Id, string RoleCode, string RoleName, string? Description);