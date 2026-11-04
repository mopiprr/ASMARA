
using ASMARA.API.Data;
using AccountEntity = ASMARA.API.Entities.Account;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Account.Commands.ChangePassword;

public class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    private readonly AsmaraDbContext _context;
    private readonly IPasswordHasher<AccountEntity> _passwordHasher;

    public ChangePasswordCommandHandler(
        AsmaraDbContext context,
        IPasswordHasher<AccountEntity> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<ChangePasswordResult> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        // Find account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == request.AccountId,
                cancellationToken);

        if (account is null || !account.IsActive)
            return ChangePasswordResult.InvalidCurrentPassword;

        // Verify current password
        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                account,
                account.PasswordHashed,
                request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
            return ChangePasswordResult.InvalidCurrentPassword;

        // Prevent using the same password
        if (request.CurrentPassword == request.NewPassword)
            return ChangePasswordResult.SamePassword;

        // Hash new password
        account.PasswordHashed = _passwordHasher.HashPassword(
            account,
            request.NewPassword);

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        return ChangePasswordResult.Success;
    }
}
