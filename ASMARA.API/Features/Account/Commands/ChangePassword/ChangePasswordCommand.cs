using MediatR;

namespace ASMARA.API.Features.Account.Commands.ChangePassword;

public class ChangePasswordCommand : IRequest<ChangePasswordResult>
{
    public Guid AccountId { get; set; }

    public string CurrentPassword { get; set; } = null!;

    public string NewPassword { get; set; } = null!;
}

public enum ChangePasswordResult
{
    Success,
    InvalidCurrentPassword,
    SamePassword
}