namespace ASMARA.API.Models.Auth;

public class AccountDto
{
    
}

public record CreateAccountRequestDto(
    Guid EmployeeId,
    string UserName,
    string Password,
    List<int>? RoleIds = null
    );

public record UpdateAccountDto(
    bool IsActive,
    List<int>? RoleIds = null
);

public record ChangePasswordRequestDto(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    string UserName,
    bool IsActive,
    DateTime? CreatedAt,
    List<RoleResponseDto>? Roles
);
    