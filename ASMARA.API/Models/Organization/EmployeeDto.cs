using ASMARA.API.Models.Auth;

namespace ASMARA.API.Models.Organization;

public class EmployeeDto
{
    
}

public record HandlerOptionDto(Guid EmployeeId, string EmployeeNumber, string FullName);

public record ManagerOptionDto(Guid Id, string EmployeeNumber, string FullName, string DepartmentName);

public record EmployeeListItemDto(
    Guid Id,
    string EmployeeNumber,
    string FullName,
    string Email,
    string DepartmentName,
    string? ManagerName,
    byte EmployeeStatus,
    string? UserName,
    bool IsAccountActive);

public record EmployeeAccountDto(
    Guid Id,
    string UserName,
    bool IsActive,
    DateTime CreatedAt,
    List<RoleOptionDto> Roles);

public record EmployeeAssetDto(int Id, string AssetTag, string ItemName, byte Status);

public record EmployeeDetailDto(
    Guid Id,
    string EmployeeNumber,
    string FullName,
    string Email,
    Guid DepartmentId,
    string DepartmentName,
    Guid? ManagerId,
    string? ManagerName,
    byte EmployeeStatus,
    DateTime HireDate,
    DateTime CreatedAt,
    DateTime? TerminatedAt,
    int SubordinateCount,
    EmployeeAccountDto? Account,
    List<EmployeeAssetDto> Assets);

public record CreateEmployeeRequestDto(
    Guid DepartmentId,
    Guid? ManagerId,
    string EmployeeNumber,
    string FullName,
    string Email,
    DateOnly HireDate,
    string UserName,
    string Password,
    List<int>? RoleIds);

public record EmployeeOptionDto(Guid Id, string EmployeeNumber, string FullName, string DepartmentName);