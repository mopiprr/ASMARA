namespace ASMARA.API.Models.Organization;

public class EmployeeDto
{
    
}

public record CreateEmployeeRequest(
    Guid DepartmentId,
    string EmployeeNumber,
    string FullName,
    string Email,
    byte EmployeeStatus,
    DateTime HireDate,
    Guid? ManagerId = null
    );
    
    public record UpdateEmployeeRequest(
        Guid DepartmentId,
        string FullName,
        string Email,
        byte EmployeeStatus,
        DateTime HireDate,
        Guid? ManagerId = null,
        DateTime? TerminatedAt = null
    );

    public record EmployeeResponse(
        Guid Id,
        Guid DepartmentId,
        string? DepartmentName,
        Guid ManagerId,
        string? ManagerName,
        string FullName,
        string Email,
        byte EmployeeStatus,
        DateTime HireDate,
        DateTime CreatedAt,
        DateTime? TerminatedAt
    );