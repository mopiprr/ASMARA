namespace ASMARA.API.Models.Organization;

public class DepartmentDto
{
    
}

public record CreateDepartmentRequest(
    string DepartmentCode,
    string DepartmentName,
    Guid? ParentDepartmentId = null
);

public record UpdateDepartmentRequest(
    string DepartmentName,
    Guid? ParentDepartmentId = null,
    bool IsActive = true
);

public record DepartmentResponse(
    Guid Id,
    string DepartmentCode,
    string DepartmentName,
    Guid? ParentDepartmentId,
    string? ParentDepartmentName,
    bool IsActive
);