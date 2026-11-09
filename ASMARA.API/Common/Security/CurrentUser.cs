
using ASMARA.API.Common.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ASMARA.API.Common.Security;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User =>
        accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated
        => User.Identity?.IsAuthenticated == true;

    public Guid AccountId
        => Guid.TryParse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : throw new UnauthorizedException(
                "No authenticated account in the current request.");

    public Guid EmployeeId
        => Guid.TryParse(
            User.FindFirstValue("employee_id"),
            out var id)
            ? id
            : throw new UnauthorizedException(
                "No authenticated employee in the current request.");

    public string FullName
        => User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? string.Empty;

    public IReadOnlyCollection<string> Roles
        => User.Claims
            .Where(c =>
                c.Type == "role" ||
                c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct()
            .ToList();

    public bool IsInRole(string role)
        => Roles.Contains(role);
}
