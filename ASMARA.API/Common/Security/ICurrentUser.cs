namespace ASMARA.API.Common.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid AccountId { get; }
    Guid EmployeeId { get; }
    string FullName { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}
