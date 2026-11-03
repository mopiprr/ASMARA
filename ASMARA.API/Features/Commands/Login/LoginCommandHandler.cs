
using Asmara.API.Data;
using Asmara.API.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Asmara.API.Features.Commands.Login;

public class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly AsmaraDbContext _context;
    private readonly IPasswordHasher<Account> _passwordHasher;
    private readonly IConfiguration _configuration;

    public LoginCommandHandler(
        AsmaraDbContext context,
        IPasswordHasher<Account> passwordHasher,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var account = await _context.Accounts
            .Include(a => a.Employee)
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(
                a => a.Employee.Email == email,
                cancellationToken);

        if (account is null || !account.IsActive)
            return null;

        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                account,
                account.PasswordHashed,
                request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
            return null;

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

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException(
                "JWT SecretKey belum dikonfigurasi.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT Issuer belum dikonfigurasi.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT Audience belum dikonfigurasi.");

        var expiresMinutes =
            _configuration.GetValue<int>("Jwt:ExpiresMinutes");

        if (expiresMinutes <= 0)
            throw new InvalidOperationException(
                "JWT ExpiresMinutes harus lebih dari 0.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(expiresMinutes);

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

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

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
