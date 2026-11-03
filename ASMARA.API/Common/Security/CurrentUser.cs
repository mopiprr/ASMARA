using System.Security.Claims;
using System.Security.Principal;

namespace ASMARA.API.Common.Security;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated 
        => User.Identity?.IsAuthenticated == true;

    public Guid EmployeeId 
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new InvalidOperationException("No authenticated user in the current request.");

    public string FullName 
        => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public IReadOnlyCollection<string> Roles 
        => User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    public bool IsInRole(string role) 
        => User.IsInRole(role);
}
