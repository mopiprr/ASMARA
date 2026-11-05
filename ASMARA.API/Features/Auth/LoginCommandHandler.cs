
using ASMARA.API.Data;
using ASMARA.API.Common.Security;
using AccountEntity = ASMARA.API.Entities.Account;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ASMARA.API.Features.Auth;

public class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly AsmaraDbContext _context;
    private readonly IPasswordHasher<AccountEntity> _passwordHasher;
    private readonly JwtOptions _jwtOptions;

    public LoginCommandHandler(
        AsmaraDbContext context,
        IPasswordHasher<AccountEntity> passwordHasher,
        IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<LoginResponse?> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // Find account
        var userName = request.UserName.Trim();

        var account = await _context.Accounts
            .Include(a => a.Employee)
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.UserName == userName, cancellationToken);

        if (account is null || !account.IsActive)
            return null;

        // Verify password
        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                account,
                account.PasswordHashed,
                request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
            return null;

        // Rehash password if required
        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHashed = _passwordHasher.HashPassword(
                account,
                request.Password);

            await _context.SaveChangesAsync(cancellationToken);
        }

        // Get active roles
        var roles = account.Roles
            .Where(role => role.IsActive)
            .Select(role => new UserRoleInfo
            {
                Id = role.Id,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName
            })
            .ToList();

        if (roles.Count == 0)
            return null;

        // Validate JWT configuration
        if (string.IsNullOrWhiteSpace(_jwtOptions.Key))
            throw new InvalidOperationException(
                "JWT Key belum dikonfigurasi.");

        if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer))
            throw new InvalidOperationException(
                "JWT Issuer belum dikonfigurasi.");

        if (string.IsNullOrWhiteSpace(_jwtOptions.Audience))
            throw new InvalidOperationException(
                "JWT Audience belum dikonfigurasi.");

        if (_jwtOptions.ExpiresMinutes <= 0)
            throw new InvalidOperationException(
                "JWT ExpiresMinutes harus lebih dari 0.");

        // Generate JWT
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtOptions.Key));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;

        var expiresAt = now.AddMinutes(
            _jwtOptions.ExpiresMinutes);

        // Claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Employee.Email),
            new("name", account.Employee.FullName),
            new("employee_id", account.EmployeeId.ToString()),
            new("employee_number", account.Employee.EmployeeNumber)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role.RoleCode));
        }

        // Create token
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        // Return login response
        return new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,

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
