using MediatR;

namespace ASMARA.API.Features.Auth;

public class LoginCommand : IRequest<LoginResponse?>
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}