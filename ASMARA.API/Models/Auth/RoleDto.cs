namespace ASMARA.API.Models.Auth;

public class RoleDto
{

}

//public record CreateRoleRequestDto(
//    string RoleCode,
//    string RoleName,
//    string? Description = null,
//    bool IsActive = true 
//    );

//public record UpdateRoleDto(
//    string RoleName,
//    string? Description = null,
//    bool IsActive = true 
//    );

public record RoleResponseDto(
    int Id,
    string RoleCode,
    string RoleName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt
    );