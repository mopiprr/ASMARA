using ASMARA.API.Common.Security;
using ASMARA.API.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Auth;

public record LogoutCommand(string? RefreshToken) : IRequest;

public class LogoutCommandHandler(AsmaraDbContext context, ITokenService tokenService) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return;

        var hash = tokenService.HashToken(command.RefreshToken);
        var now = DateTime.UtcNow;

        await context.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, (DateTime?)now),
                ct);
    }
}