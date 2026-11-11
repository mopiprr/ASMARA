using ASMARA.API.Common.Security;
using ASMARA.API.Data;
using Microsoft.Extensions.Options;
using AccountEntity = ASMARA.API.Entities.Account;
using RefreshTokenEntity = ASMARA.API.Entities.RefreshToken;

namespace ASMARA.API.Features.Auth;

public class AuthSessionService
{
    private readonly AsmaraDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthSessionService(
        AsmaraDbContext context,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public static List<UserRoleInfo> GetActiveRoles(AccountEntity account)
        => account.Roles
            .Where(role => role.IsActive)
            .Select(role => new UserRoleInfo
            {
                Id = role.Id,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName
            })
            .ToList();

    public async Task<LoginResponse> CreateSessionAsync(AccountEntity account, List<UserRoleInfo> roles, bool isPersistent, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var (accessToken, expiresAt) = _tokenService.CreateAccessToken(
            account,
            roles.Select(role => role.RoleCode));

        var refreshToken = _tokenService.CreateRefreshToken();
        var refreshExpiresAt = now.AddMinutes(_jwtOptions.RefreshTokenMinutes);

        _context.RefreshTokens.Add(new RefreshTokenEntity
        {
            AccountId = account.Id,
            TokenHash = _tokenService.HashToken(refreshToken),
            IsPersistent = isPersistent,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,

            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshExpiresAt,
            IsPersistent = isPersistent,

            User = new LoginUserInfo
            {
                AccountId = account.Id,
                EmployeeId = account.EmployeeId,
                EmployeeNumber = account.Employee.EmployeeNumber,
                FullName = account.Employee.FullName,
                Email = account.Employee.Email,
                Roles = roles
            }
        };
    }
}