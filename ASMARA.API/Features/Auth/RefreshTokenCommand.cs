using ASMARA.API.Common.Security;
using ASMARA.API.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Auth;

public record RefreshTokenCommand(string? RefreshToken) : IRequest<LoginResponse?>;

public class RefreshTokenCommandHandler(AsmaraDbContext context, ITokenService tokenService, AuthSessionService sessionService)
        : IRequestHandler<RefreshTokenCommand, LoginResponse?>
{
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    public async Task<LoginResponse?> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return null;

        var hash = tokenService.HashToken(command.RefreshToken);
        var now = DateTime.UtcNow;

        var stored = await context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            return null;

        if (stored.RevokedAt is not null)
        {
            if (stored.RevokedAt < now - ReuseGracePeriod)
            {
                await context.RefreshTokens
                    .Where(t => t.AccountId == stored.AccountId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(t => t.RevokedAt, (DateTime?)now),
                        ct);
            }

            return null;
        }

        if (stored.ExpiresAt <= now)
            return null;

        var revokedCount = await context.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, (DateTime?)now),
                ct);

        if (revokedCount == 0)
            return null;

        var account = await context.Accounts
            .Include(a => a.Employee)
            .Include(a => a.Roles)
            .FirstOrDefaultAsync(a => a.Id == stored.AccountId, ct);

        if (account is null || !account.IsActive)
            return null;

        var roles = AuthSessionService.GetActiveRoles(account);

        if (roles.Count == 0)
            return null;

        return await sessionService.CreateSessionAsync(
            account,
            roles,
            stored.IsPersistent,
            ct);
    }
}