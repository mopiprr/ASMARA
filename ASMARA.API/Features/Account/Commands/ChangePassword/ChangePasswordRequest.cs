namespace ASMARA.API.Features.Account.Commands.ChangePassword;

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = null!;

    public string NewPassword { get; set; } = null!;
}