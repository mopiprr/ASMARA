using ASMARA.API.Data;
using AccountEntity = ASMARA.API.Entities.Account;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ASMARA.API.Services.Auth;

namespace ASMARA.API.Features.Auth;

public class LoginCommandHandler(AsmaraDbContext context, IPasswordHasher<AccountEntity> passwordHasher, AuthSessionService sessionService)
        : IRequestHandler<LoginCommand, LoginResponse?>
{

    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken ct)
    {
        // Find account
        var userName = request.UserName.Trim();

        var account = await context.Accounts
            .Include(a => a.Employee)
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.UserName == userName, ct);

        if (account is null || !account.IsActive)
            return null;

        // Verify password
        var verificationResult =
            passwordHasher.VerifyHashedPassword(
                account,
                account.PasswordHashed,
                request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
            return null;

        // Rehash password if required (ikut tersimpan di CreateSessionAsync)
        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHashed = passwordHasher.HashPassword(
                account,
                request.Password);
        }

        // Get active roles
        var roles = AuthSessionService.GetActiveRoles(account);

        if (roles.Count == 0)
            return null;

        // Remember me -> cookie persistent
        return await sessionService.CreateSessionAsync(
            account,
            roles,
            request.RememberMe,
            ct);
    }
}