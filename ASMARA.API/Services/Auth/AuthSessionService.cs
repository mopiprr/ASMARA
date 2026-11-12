using ASMARA.API.Common.Security;
using ASMARA.API.Data;
using ASMARA.API.Features.Auth;
using Microsoft.Extensions.Options;
using AccountEntity = ASMARA.API.Entities.Account;
using RefreshTokenEntity = ASMARA.API.Entities.RefreshToken;

namespace ASMARA.API.Services.Auth;

public class AuthSessionService(AsmaraDbContext context, ITokenService tokenService, IOptions<JwtOptions> jwtOptions)
{

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

        var (accessToken, expiresAt) = tokenService.CreateAccessToken(
            account,
            roles.Select(role => role.RoleCode));

        var refreshToken = tokenService.CreateRefreshToken();
        var refreshExpiresAt = now.AddMinutes(jwtOptions.Value.RefreshTokenMinutes);

        context.RefreshTokens.Add(new RefreshTokenEntity
        {
            AccountId = account.Id,
            TokenHash = tokenService.HashToken(refreshToken),
            IsPersistent = isPersistent,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        });

        await context.SaveChangesAsync(cancellationToken);

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