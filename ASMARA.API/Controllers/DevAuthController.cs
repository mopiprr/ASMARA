using System.Security.Claims;
using System.Text;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ASMARA.API.Controllers;

/// KHUSUS TESTING: buat token tanpa password. Hanya aktif di Development.
/// Akan digantikan oleh Login milik teman. Hapus sebelum rilis.
[ApiController]
[Route("api/dev/token")]
[AllowAnonymous]
public class DevAuthController(
    IWebHostEnvironment env,
    IAccountRepository accounts,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("{employeeNumber}")]
    public async Task<IActionResult> CreateToken(string employeeNumber, CancellationToken ct)
    {
        if (!env.IsDevelopment()) return NotFound();

        var account = await accounts.QueryNoTracking()
            .Where(a => a.Employee.EmployeeNumber == employeeNumber && a.IsActive)
            .Select(a => new
            {
                a.EmployeeId,
                a.Employee.FullName,
                Roles = a.Roles.Select(r => r.RoleCode).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (account is null) return NotFound($"No active account for {employeeNumber}.");

        // Claim harus sama dengan yang dibaca CurrentUser
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.EmployeeId.ToString()),
            new(ClaimTypes.Name, account.FullName)
        };
        claims.AddRange(account.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var jwt = jwtOptions.Value;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(jwt.ExpiresMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        });

        return Ok(new { account.FullName, account.Roles, Token = token });
    }
}