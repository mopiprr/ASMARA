using ASMARA.API.Common.Security;
using ASMARA.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ASMARA.API.Services.Auth;

public class RefreshTokenCleanupService(IServiceScopeFactory scopeFactory, IOptions<JwtOptions> jwtOptions, ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(
            Math.Max(1, jwtOptions.Value.RefreshTokenCleanupMinutes));

        using var timer = new PeriodicTimer(interval);

        do
        {
            await CleanupAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmaraDbContext>();

            var now = DateTime.UtcNow;

            var deleted = await context.RefreshTokens
                .Where(t => t.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} expired refresh token(s).", deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean up expired refresh tokens.");
        }
    }
}