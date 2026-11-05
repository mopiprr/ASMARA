using MediatR;

namespace ASMARA.API.Features.Auth;

public class LoginCommand : IRequest<LoginResponse?>
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}